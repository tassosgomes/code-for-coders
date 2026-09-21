using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CodeForCoders.Notification.Infra.Data;

public sealed class DeliveryRecordPurgeWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DeliveryRecordRetentionOptions> options,
    ILogger<DeliveryRecordPurgeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.GetPollingInterval());
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await PurgeExpiredRecordsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (NpgsqlException exception)
                {
                    logger.LogWarning(
                        exception,
                        "Delivery record personal-data purge failed because PostgreSQL is unavailable.");
                }
                catch (DbUpdateException exception)
                {
                    logger.LogWarning(
                        exception,
                        "Delivery record personal-data purge failed while persisting changes.");
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogWarning(
                        exception,
                        "Delivery record personal-data purge failed because the data context is unavailable.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PurgeExpiredRecordsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.RetentionDays);
        var records = await dbContext.DeliveryRecords
            .IgnoreQueryFilters()
            .Where(record =>
                (record.Status == DeliveryStatus.Refused && record.RefusedOn <= cutoff)
                || (record.Status == DeliveryStatus.Delivered && record.DeliveredOn <= cutoff)
                || (record.Status == DeliveryStatus.Failed && record.FailedOn <= cutoff))
            .Where(record =>
                record.Recipient != null
                || record.RecipientName != null
                || record.Link != null
                || record.Reason != null)
            .OrderBy(record => record.Id)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            record.PurgePersonalData();
        }

        if (records.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
