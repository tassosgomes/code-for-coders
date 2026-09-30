using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Contracts;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Media.Application.UseCases.Videos.PrepareVideo;

public sealed class PrepareVideo(
    IVideoPreparationRepository videos,
    IMediaStoragePort storage,
    IVideoTranscoder transcoder,
    IVideoKeyProtector keyProtector,
    IOutboxMessageWriter outbox,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<PrepareVideo> logger) : IVideoPreparationWorkflow
{
    public async Task<bool> ExecuteNextAsync(
        string workDirectory,
        long availableDiskBytes,
        TimeSpan leaseDuration,
        TimeSpan leaseRenewalInterval,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lease = await videos.ClaimNextAsync(now, leaseDuration, availableDiskBytes, cancellationToken);
        if (lease is null)
        {
            return false;
        }

        MediaTelemetry.VideosClaimed.Add(1);
        MediaTelemetry.VideoWait.Record(Math.Max(0, (now - lease.UploadedAt).TotalSeconds));
        if (lease.PreparationAttempts > 1)
        {
            MediaTelemetry.VideosRetried.Add(1);
        }

        await PrepareClaimedVideoAsync(lease, workDirectory, leaseDuration, leaseRenewalInterval, cancellationToken);
        return true;
    }

    public Task<int> RecoverExpiredLeasesAsync(int batchSize, CancellationToken cancellationToken)
        => RecoverExpiredLeasesCoreAsync(batchSize, cancellationToken);

    public async Task<int> CleanupFinalArtifactsAsync(int batchSize, CancellationToken cancellationToken)
    {
        var cleanups = await videos.GetFinalOriginalsForCleanupAsync(batchSize, cancellationToken);
        var deleted = 0;
        foreach (var cleanup in cleanups)
        {
            using var activity = StartActivity("media.video.delete-original", cleanup.VideoId);
            if (cleanup.Status == "failed")
            {
                await storage.DeletePrefixAsync(GetHlsObjectPrefix(cleanup.OriginalObjectKey), cancellationToken);
            }

            await storage.DeleteObjectAsync(cleanup.OriginalObjectKey, cancellationToken);
            if (await videos.MarkOriginalDeletedAsync(cleanup.VideoId, timeProvider.GetUtcNow(), cancellationToken))
            {
                await unitOfWork.CommitAsync(cancellationToken);
                deleted++;
            }
        }

        return deleted;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Unexpected per-item preparation failures are persisted and retried with a bounded policy.")]
    private async Task PrepareClaimedVideoAsync(
        VideoPreparationLease lease,
        string workDirectory,
        TimeSpan leaseDuration,
        TimeSpan leaseRenewalInterval,
        CancellationToken stoppingToken)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object?> { ["VideoId"] = lease.VideoId });
        var videoDirectory = Path.Combine(workDirectory, lease.VideoId.ToString("N"));
        var sourcePath = Path.Combine(videoDirectory, "original.mp4");
        var hlsDirectory = Path.Combine(videoDirectory, "hls");
        var keyPath = Path.Combine(videoDirectory, "video-key.bin");
        var keyInfoPath = Path.Combine(videoDirectory, "key-info.txt");
        var videoKey = RandomNumberGenerator.GetBytes(16);
        using var processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(processingCancellation.Token);
        Task leaseRenewal = Task.CompletedTask;

        try
        {
            try
            {
                Directory.CreateDirectory(videoDirectory);
                Directory.CreateDirectory(hlsDirectory);
                await WritePrivateFileAsync(keyPath, videoKey, processingCancellation.Token);
                await File.WriteAllLinesAsync(
                    keyInfoPath,
                    [$"c4c-key:{lease.VideoId:D}", keyPath],
                    processingCancellation.Token);
                SetPrivateFileMode(keyInfoPath);
                leaseRenewal = RenewLeaseUntilCanceledAsync(
                    lease,
                    leaseDuration,
                    leaseRenewalInterval,
                    processingCancellation,
                    renewalCancellation.Token);

                var downloadStartedAt = Stopwatch.GetTimestamp();
                try
                {
                    using (StartActivity("media.video.download", lease.VideoId))
                    {
                        await storage.DownloadObjectAsync(lease.OriginalObjectKey, sourcePath, processingCancellation.Token);
                    }
                }
                finally
                {
                    MediaTelemetry.RecordVideoPrepareDuration("download", downloadStartedAt);
                }

                await storage.DeletePrefixAsync(
                    GetHlsObjectPrefix(lease.OriginalObjectKey),
                    processingCancellation.Token);
                var result = await transcoder.TranscodeAsync(
                    lease.VideoId,
                    sourcePath,
                    hlsDirectory,
                    keyInfoPath,
                    processingCancellation.Token);
                if (result.FailureReason is not null)
                {
                    renewalCancellation.Cancel();
                    await leaseRenewal;
                    await RecordFailureAsync(lease, result.FailureReason, stoppingToken);
                    return;
                }

                var protectedKey = keyProtector.Protect(lease.VideoId, videoKey);

                var publishStartedAt = Stopwatch.GetTimestamp();
                try
                {
                    using (StartActivity("media.video.publish", lease.VideoId))
                    {
                        await storage.UploadDirectoryAsync(
                            hlsDirectory,
                            GetHlsObjectPrefix(lease.OriginalObjectKey),
                            processingCancellation.Token);
                    }
                }
                finally
                {
                    MediaTelemetry.RecordVideoPrepareDuration("publish", publishStartedAt);
                }

                renewalCancellation.Cancel();
                await leaseRenewal;
                processingCancellation.Token.ThrowIfCancellationRequested();
                await CompletePreparationAsync(lease, result, protectedKey, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "A video preparation attempt failed and will be retried when eligible.");
                renewalCancellation.Cancel();
                await leaseRenewal;
                await RecordFailureAsync(lease, VideoFailureReasons.PreparationFailed, stoppingToken);
                return;
            }

            try
            {
                await storage.DeleteObjectAsync(lease.OriginalObjectKey, stoppingToken);
                if (await videos.MarkOriginalDeletedAsync(lease.VideoId, timeProvider.GetUtcNow(), stoppingToken))
                {
                    await unitOfWork.CommitAsync(stoppingToken);
                }
            }
            catch (StorageUnavailableException exception)
            {
                logger.LogWarning(exception, "The prepared video original will be deleted by a later cleanup cycle.");
            }
        }
        finally
        {
            processingCancellation.Cancel();
            renewalCancellation.Cancel();
            try
            {
                await leaseRenewal;
            }
            catch (OperationCanceledException) when (processingCancellation.IsCancellationRequested)
            {
            }

            CryptographicOperations.ZeroMemory(videoKey);
            TryDeleteWorkDirectory(videoDirectory);
        }
    }

    private async Task CompletePreparationAsync(
        VideoPreparationLease lease,
        VideoTranscodeResult result,
        ProtectedVideoKey protectedKey,
        CancellationToken cancellationToken)
    {
        var video = await videos.GetClaimedAsync(lease.VideoId, lease.LeaseId, cancellationToken);
        if (video is null)
        {
            throw new InvalidOperationException("The video preparation lease is no longer active.");
        }

        var occurredAt = timeProvider.GetUtcNow();
        var eventId = Guid.CreateVersion7(occurredAt);
        video.MarkReady(lease.LeaseId, result.DurationSeconds, result.StoredBytes, protectedKey.Ciphertext, protectedKey.MasterKeyId);
        await outbox.AppendAsync(
            new OutboxMessageDraft(
                eventId,
                video.TenantId,
                "AtivoProntoV1",
                "midia.ativo-pronto.v1",
                new AtivoProntoV1(eventId, video.TenantId, video.VideoId, occurredAt, result.DurationSeconds),
                occurredAt,
                lease.CorrelationId),
            cancellationToken);
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            throw new InvalidOperationException("The video preparation lease was lost before the ready commit.");
        }

        MediaTelemetry.VideosCompleted.Add(1);
        MediaTelemetry.VideoTimeToReady.Record(Math.Max(0, (occurredAt - lease.UploadedAt).TotalSeconds));
    }

    private async Task<int> RecoverExpiredLeasesCoreAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var expiredLeases = await videos.GetExpiredLeasesAsync(now, batchSize, cancellationToken);
        var recovered = 0;
        foreach (var lease in expiredLeases)
        {
            try
            {
                if (await RecordFailureAsync(lease, VideoFailureReasons.PreparationFailed, cancellationToken))
                {
                    recovered++;
                }
            }
            catch (ConcurrencyConflictException)
            {
                // A different worker already recovered or completed this lease.
            }
        }

        return recovered;
    }

    private async Task<bool> RecordFailureAsync(
        VideoPreparationLease lease,
        string reason,
        CancellationToken cancellationToken)
    {
        var video = await videos.GetClaimedAsync(lease.VideoId, lease.LeaseId, cancellationToken);
        if (video is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (reason == VideoFailureReasons.PreparationFailed
            && video.PreparationAttempts < Video.MaximumPreparationAttempts)
        {
            video.SchedulePreparationRetry(lease.LeaseId, now.Add(GetRetryDelay(video.PreparationAttempts)));
            await unitOfWork.CommitAsync(cancellationToken);
            await TryDeletePartialHlsAsync(lease, cancellationToken);
            return true;
        }

        video.MarkFailed(lease.LeaseId, reason);
        var eventId = Guid.CreateVersion7(now);
        await outbox.AppendAsync(
            new OutboxMessageDraft(
                eventId,
                video.TenantId,
                "PreparacaoFalhouV1",
                "midia.preparacao-falhou.v1",
                new PreparacaoFalhouV1(eventId, video.TenantId, video.VideoId, now, reason),
                now,
                lease.CorrelationId),
            cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        MediaTelemetry.VideosFailed.Add(
            1,
            new KeyValuePair<string, object?>("reason", GetFailureMetricReason(video.FailureReason)));
        await TryCleanupFailedArtifactsAsync(lease, cancellationToken);
        return true;
    }

    private async Task TryDeletePartialHlsAsync(VideoPreparationLease lease, CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeletePrefixAsync(GetHlsObjectPrefix(lease.OriginalObjectKey), cancellationToken);
        }
        catch (StorageUnavailableException exception)
        {
            logger.LogWarning(exception, "Partial HLS objects will be removed before the next preparation attempt.");
        }
    }

    private async Task TryCleanupFailedArtifactsAsync(
        VideoPreparationLease lease,
        CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeletePrefixAsync(GetHlsObjectPrefix(lease.OriginalObjectKey), cancellationToken);
            await storage.DeleteObjectAsync(lease.OriginalObjectKey, cancellationToken);
            if (await videos.MarkOriginalDeletedAsync(lease.VideoId, timeProvider.GetUtcNow(), cancellationToken))
            {
                await unitOfWork.CommitAsync(cancellationToken);
            }
        }
        catch (StorageUnavailableException exception)
        {
            logger.LogWarning(exception, "Failed video artifacts will be removed by a later cleanup cycle.");
        }
    }

    private static TimeSpan GetRetryDelay(int attempts)
        => attempts switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            _ => throw new InvalidOperationException("The video preparation retry count is invalid."),
        };

    private static string GetFailureMetricReason(string? reason)
        => reason switch
        {
            VideoFailureReasons.UnreadableFile => "unreadable-file",
            VideoFailureReasons.UnsupportedFormat => "unsupported-format",
            VideoFailureReasons.DurationExceeded => "duration-exceeded",
            VideoFailureReasons.PreparationFailed => "attempts-exhausted",
            _ => "attempts-exhausted",
        };

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A lease renewal failure must stop this preparation while allowing the worker host to continue.")]
    private async Task RenewLeaseUntilCanceledAsync(
        VideoPreparationLease lease,
        TimeSpan leaseDuration,
        TimeSpan renewalInterval,
        CancellationTokenSource processingCancellation,
        CancellationToken renewalToken)
    {
        using var timer = new PeriodicTimer(renewalInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(renewalToken))
            {
                var renewed = await videos.RenewLeaseAsync(
                    lease.VideoId,
                    lease.LeaseId,
                    timeProvider.GetUtcNow().Add(leaseDuration),
                    renewalToken);
                if (!renewed)
                {
                    processingCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (renewalToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The video preparation lease could not be renewed; processing was stopped.");
            processingCancellation.Cancel();
        }
    }

    private static string GetHlsObjectPrefix(string originalObjectKey)
    {
        var originalNameIndex = originalObjectKey.LastIndexOf("/original", StringComparison.Ordinal);
        if (originalNameIndex < 0)
        {
            throw new InvalidOperationException("The video source object key has an invalid shape.");
        }

        return $"{originalObjectKey[..originalNameIndex]}/hls";
    }

    private static async Task WritePrivateFileAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        SetPrivateFileMode(path);
    }

    private static void SetPrivateFileMode(string path)
    {
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private void TryDeleteWorkDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The video preparation work directory could not be removed.");
        }
    }

    private static Activity? StartActivity(string name, Guid videoId)
    {
        var activity = MediaTelemetry.ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("video.id", videoId.ToString("D"));
        activity?.SetTag("video.status", "preparing");
        return activity;
    }

}
