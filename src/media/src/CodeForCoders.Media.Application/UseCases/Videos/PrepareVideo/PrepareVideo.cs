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

        await PrepareClaimedVideoAsync(lease, workDirectory, leaseDuration, leaseRenewalInterval, cancellationToken);
        return true;
    }

    public Task<int> RecoverExpiredLeasesAsync(int batchSize, CancellationToken cancellationToken)
        => videos.ReleaseExpiredLeasesAsync(timeProvider.GetUtcNow(), batchSize, cancellationToken);

    public async Task<int> CleanupReadyOriginalsAsync(int batchSize, CancellationToken cancellationToken)
    {
        var cleanups = await videos.GetReadyOriginalsForCleanupAsync(batchSize, cancellationToken);
        var deleted = 0;
        foreach (var cleanup in cleanups)
        {
            using var activity = StartActivity("media.video.delete-original");
            await storage.DeleteObjectAsync(cleanup.OriginalObjectKey, cancellationToken);
            if (await videos.MarkOriginalDeletedAsync(cleanup.VideoId, timeProvider.GetUtcNow(), cancellationToken))
            {
                await unitOfWork.CommitAsync(cancellationToken);
                deleted++;
            }
        }

        return deleted;
    }

    private async Task PrepareClaimedVideoAsync(
        VideoPreparationLease lease,
        string workDirectory,
        TimeSpan leaseDuration,
        TimeSpan leaseRenewalInterval,
        CancellationToken stoppingToken)
    {
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

            using (StartActivity("media.video.download"))
            {
                await storage.DownloadObjectAsync(lease.OriginalObjectKey, sourcePath, processingCancellation.Token);
            }

            var result = await transcoder.TranscodeAsync(
                sourcePath,
                hlsDirectory,
                keyInfoPath,
                processingCancellation.Token);
            var protectedKey = keyProtector.Protect(lease.VideoId, videoKey);

            using (StartActivity("media.video.publish"))
            {
                await storage.UploadDirectoryAsync(
                    hlsDirectory,
                    GetHlsObjectPrefix(lease.OriginalObjectKey),
                    processingCancellation.Token);
            }

            renewalCancellation.Cancel();
            await leaseRenewal;
            processingCancellation.Token.ThrowIfCancellationRequested();
            await CompletePreparationAsync(lease, result, protectedKey, stoppingToken);

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
    }

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

    private static Activity? StartActivity(string name)
    {
        var activity = MediaTelemetry.ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("video.status", "preparing");
        return activity;
    }

}
