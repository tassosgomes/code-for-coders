using CodeForCoders.BffStudent.Infra.Data;
using CodeForCoders.BffStudent.Infra.Data.Configuration;
using CodeForCoders.BffStudent.Infra.Data.Outbox;
using CodeForCoders.BffStudent.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CodeForCoders.BffStudent.Infra.Messaging;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    RabbitMqPublisher publisher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    for (var index = 0; index < options.Value.BatchSize; index++)
                    {
                        if (!await PublishOneAsync(stoppingToken))
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (OutboxPublishException exception)
                {
                    logger.LogWarning(exception, "Outbox publication failed and will be retried.");
                }
                catch (NpgsqlException exception)
                {
                    logger.LogWarning(exception, "Outbox polling failed because PostgreSQL is unavailable.");
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogWarning(exception, "Outbox polling failed because the data context is unavailable.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> PublishOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BffStudentDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var maxAttempts = options.Value.MaxAttempts;
        var query = $"""
            SELECT * FROM {BffStudentSchema.Name}.outbox_messages
            WHERE processed_on IS NULL AND attempts < {maxAttempts}
            ORDER BY id
            LIMIT 1
            FOR UPDATE SKIP LOCKED
            """;
        var message = await dbContext.OutboxMessages
            .FromSqlRaw(query)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        try
        {
            await publisher.PublishAsync(message, cancellationToken);
            message.MarkProcessed();
            await dbContext.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch (OutboxPublishException exception)
        {
            message.RegisterFailure(exception);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
            throw;
        }

        return true;
    }
}
