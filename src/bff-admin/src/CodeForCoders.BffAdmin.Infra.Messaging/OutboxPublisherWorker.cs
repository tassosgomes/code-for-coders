using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.BffAdmin.Infra.Data;
using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using CodeForCoders.BffAdmin.Infra.Data.Outbox;
using CodeForCoders.BffAdmin.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CodeForCoders.BffAdmin.Infra.Messaging;

public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    RabbitMqPublisher publisher,
    OutboxPayloadProtector payloadProtector,
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
        var message = await LeaseNextAsync(cancellationToken);
        if (message is null)
        {
            return false;
        }

        string payload;
        try
        {
            payload = payloadProtector.Unprotect(message);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            await RecordFailureAsync(message.Id, message.LeaseToken!.Value, "OUTBOX_PAYLOAD_UNREADABLE");
            throw new OutboxPublishException("The leased outbox payload could not be decrypted.", exception);
        }

        try
        {
            await publisher.PublishAsync(message, payload, cancellationToken);
        }
        catch (OutboxPublishException exception) when (exception.BrokerUnavailable)
        {
            await ReleaseLeaseAsync(message.Id, message.LeaseToken!.Value, "OUTBOX_BROKER_UNAVAILABLE");
            throw;
        }
        catch (OutboxPublishException)
        {
            await RecordFailureAsync(message.Id, message.LeaseToken!.Value, "OUTBOX_PUBLISH_FAILED");
            throw;
        }

        await MarkProcessedAsync(message.Id, message.LeaseToken!.Value);
        return true;
    }

    private async Task<OutboxMessage?> LeaseNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BffAdminDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var query = $"""
            SELECT * FROM {BffAdminSchema.Name}.outbox_messages
            WHERE processed_on IS NULL
                AND attempts < @max_attempts
                AND (lease_expires_on IS NULL OR lease_expires_on <= @lease_time)
            ORDER BY id
            LIMIT 1
            FOR UPDATE SKIP LOCKED
            """;
        var now = DateTimeOffset.UtcNow;
        var message = await dbContext.OutboxMessages
            .FromSqlRaw(
                query,
                new NpgsqlParameter("max_attempts", options.Value.MaxAttempts),
                new NpgsqlParameter("lease_time", now))
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        message.AcquireLease(
            Guid.CreateVersion7(),
            now.AddSeconds(options.Value.LeaseDurationSeconds));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return message;
    }

    private async Task MarkProcessedAsync(Guid messageId, Guid leaseToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BffAdminDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(message => message.Id == messageId && message.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.ProcessedOn, DateTimeOffset.UtcNow)
                .SetProperty(message => message.LeaseToken, (Guid?)null)
                .SetProperty(message => message.LeaseExpiresOn, (DateTimeOffset?)null), CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
    }

    // Broker unavailability says nothing about the message itself, so the lease is released without
    // spending its attempts; the message is published once the broker is back, however long the outage.
    private async Task ReleaseLeaseAsync(Guid messageId, Guid leaseToken, string errorCode)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BffAdminDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(message => message.Id == messageId && message.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.LastError, errorCode)
                .SetProperty(message => message.LeaseToken, (Guid?)null)
                .SetProperty(message => message.LeaseExpiresOn, (DateTimeOffset?)null), CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
    }

    private async Task RecordFailureAsync(Guid messageId, Guid leaseToken, string errorCode)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BffAdminDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(message => message.Id == messageId && message.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Attempts, message => message.Attempts + 1)
                .SetProperty(message => message.LastError, errorCode)
                .SetProperty(message => message.LeaseToken, (Guid?)null)
                .SetProperty(message => message.LeaseExpiresOn, (DateTimeOffset?)null), CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
    }
}
