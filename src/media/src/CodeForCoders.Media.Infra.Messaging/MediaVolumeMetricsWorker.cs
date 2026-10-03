using System.Diagnostics.Metrics;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class MediaVolumeMetricsWorker : BackgroundService
{
    private static readonly string[] KnownStatuses = ["received", "preparing", "ready", "failed"];
    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<MediaVolumeMetricsWorker> logger;
    private readonly int outboxMaxAttempts;
    private MetricsSnapshot snapshot = new(0, 0, [], 0, 0, 0, 0, 0, 0);

    public MediaVolumeMetricsWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<MediaVolumeMetricsWorker> logger,
        IOptions<OutboxOptions> outboxOptions)
    {
        this.scopeFactory = scopeFactory;
        this.timeProvider = timeProvider;
        this.logger = logger;
        outboxMaxAttempts = outboxOptions.Value.MaxAttempts;
        MediaTelemetry.Meter.CreateObservableGauge("media.storage.used", () => Volatile.Read(ref snapshot).StoredBytes, unit: "By");
        MediaTelemetry.Meter.CreateObservableGauge("media.videos.count", () => Volatile.Read(ref snapshot).Counts);
        MediaTelemetry.Meter.CreateObservableGauge("media.videos.stuck", () => Volatile.Read(ref snapshot).StuckCount, unit: "{video}");
        MediaTelemetry.Meter.CreateObservableGauge("media.uploads.pending", () => Volatile.Read(ref snapshot).PendingUploads, unit: "{upload}");
        MediaTelemetry.Meter.CreateObservableGauge("media.videos.oldest_waiting", () => Volatile.Read(ref snapshot).OldestWaitingSeconds, unit: "s");
        MediaTelemetry.Meter.CreateObservableGauge("media.outbox.pending", () => Volatile.Read(ref snapshot).PendingOutboxMessages, unit: "{message}");
        MediaTelemetry.Meter.CreateObservableGauge("media.outbox.oldest_pending", () => Volatile.Read(ref snapshot).OldestPendingOutboxSeconds, unit: "s");
        MediaTelemetry.Meter.CreateObservableGauge("media.outbox.exhausted", () => Volatile.Read(ref snapshot).ExhaustedOutboxMessages, unit: "{message}");
        MediaTelemetry.Meter.CreateObservableGauge("media.students.active", () => Volatile.Read(ref snapshot).ActiveStudents, unit: "{student}");
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var rows = await dbContext.Videos.IgnoreQueryFilters().AsNoTracking()
            .GroupBy(video => video.Status)
            .Select(group => new { Status = group.Key, Count = group.LongCount(), Bytes = group.Sum(video => video.StoredBytes) })
            .ToListAsync(cancellationToken);
        var pendingUploads = await dbContext.VideoUploads.IgnoreQueryFilters().AsNoTracking()
            .LongCountAsync(upload => upload.CompletedAt == null && upload.ExpiredAt == null, cancellationToken);
        var oldestWaiting = await dbContext.Videos.IgnoreQueryFilters().AsNoTracking()
            .Where(video => video.Status == "received")
            .MinAsync(video => (DateTimeOffset?)video.UploadedAt, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var oldestWaitingSeconds = oldestWaiting is null
            ? 0
            : Math.Max(0, (now - oldestWaiting.Value).TotalSeconds);
        var pendingOutbox = dbContext.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .Where(message => message.ProcessedOn == null);
        var pendingOutboxMessages = await pendingOutbox.LongCountAsync(cancellationToken);
        var oldestPendingOutbox = await pendingOutbox.MinAsync(
            message => (DateTimeOffset?)message.OccurredOn,
            cancellationToken);
        var oldestPendingOutboxSeconds = oldestPendingOutbox is null
            ? 0
            : Math.Max(0, (now - oldestPendingOutbox.Value).TotalSeconds);
        var exhaustedOutboxMessages = await pendingOutbox.LongCountAsync(
            message => message.Attempts >= outboxMaxAttempts,
            cancellationToken);
        var stuck = await dbContext.Videos.FromSqlInterpolated($"""
                SELECT * FROM media_access.videos
                WHERE status IN ('received', 'preparing')
                  AND uploaded_at < {now} - COALESCE(duration_seconds * 4 * INTERVAL '1 second', INTERVAL '12 hours')
                """)
            .IgnoreQueryFilters()
            .LongCountAsync(cancellationToken);
        var thirtyDaysAgo = now.AddDays(-30);
        var activeStudents = await dbContext.PlaybackSessions.IgnoreQueryFilters().AsNoTracking()
            .Where(session => session.CreatedAt >= thirtyDaysAgo)
            .Select(session => session.StudentId)
            .Distinct()
            .LongCountAsync(cancellationToken);
        var statusCounts = rows.ToDictionary(row => row.Status, row => row.Count, StringComparer.Ordinal);
        var counts = KnownStatuses.Select(status => new Measurement<long>(
            statusCounts.GetValueOrDefault(status),
            new KeyValuePair<string, object?>("status", status))).ToArray();
        Volatile.Write(ref snapshot, new MetricsSnapshot(
            rows.Sum(row => row.Bytes),
            stuck,
            counts,
            pendingUploads,
            oldestWaitingSeconds,
            pendingOutboxMessages,
            oldestPendingOutboxSeconds,
            exhaustedOutboxMessages,
            activeStudents));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            do
            {
                try
                {
                    await RefreshAsync(stoppingToken);
                }
                catch (NpgsqlException exception)
                {
                    logger.LogWarning(exception, "Media volume metrics collection failed because PostgreSQL is unavailable.");
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogWarning(exception, "Media volume metrics collection failed because the data context is unavailable.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private sealed record MetricsSnapshot(
        long StoredBytes,
        long StuckCount,
        Measurement<long>[] Counts,
        long PendingUploads,
        double OldestWaitingSeconds,
        long PendingOutboxMessages,
        double OldestPendingOutboxSeconds,
        long ExhaustedOutboxMessages,
        long ActiveStudents);
}
