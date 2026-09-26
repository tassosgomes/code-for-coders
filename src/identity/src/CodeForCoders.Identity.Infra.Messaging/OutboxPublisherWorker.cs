using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Configuration;
using CodeForCoders.Identity.Infra.Data.Outbox;
using CodeForCoders.Identity.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CodeForCoders.Identity.Infra.Messaging;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    RabbitMqPublisher publisher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private const string AuditActionRoutingKey = "auditoria.ato-praticado.v1";

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
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var maxAttempts = options.Value.MaxAttempts;
        // The schema is an identifier and cannot be sent as a database parameter. Both values
        // interpolated below are constants or validated numeric options, so the raw SQL stays
        // bounded to this module's outbox table.
        var query = $"""
            SELECT * FROM {IdentitySchema.Name}.outbox_messages
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
            if (message.RoutingKey == AuditActionRoutingKey)
            {
                IdentityTelemetry.AuditActionsPublished.Add(1);
            }
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
