using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.VideoUploads;
using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.CreateVideoUpload;

public sealed class CreateVideoUpload(
    ITenantContext tenantContext,
    IVideoUploadRepository videoUploads,
    IOperationIdempotencyRepository idempotencyRecords,
    IMediaStoragePort mediaStorage,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICreateVideoUpload
{
    public async Task<CreateVideoUploadOutput> ExecuteAsync(
        CreateVideoUploadInput input,
        CancellationToken cancellationToken)
    {
        var (tenantId, actorAccountId) = VideoUploadUseCaseHelpers.RequireActor(tenantContext);
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128)
        {
            throw new MediaApiException(400, "INVALID_REQUEST", "A valid idempotency key is required.");
        }

        var proposedUpload = VideoUpload.Create(
            tenantId,
            actorAccountId,
            input.Title,
            input.FileName,
            input.FileSize,
            input.ContentType,
            input.Fingerprint,
            input.UploaderName,
            now);

        var requestHash = VideoUploadUseCaseHelpers.HashRequest(input);
        var existingIdempotency = await idempotencyRecords.GetAsync(
            "createVideoUpload",
            tenantId,
            actorAccountId,
            input.IdempotencyKey,
            cancellationToken);
        if (existingIdempotency is not null && existingIdempotency.ExpiresAt > now)
        {
            VideoUploadUseCaseHelpers.EnsureSameRequest(existingIdempotency, requestHash);
            return VideoUploadUseCaseHelpers.Replay<CreateVideoUploadOutput>(existingIdempotency);
        }

        var pendingUpload = await videoUploads.GetPendingByFingerprintAsync(
            tenantId,
            actorAccountId,
            input.Fingerprint,
            now,
            cancellationToken);
        if (pendingUpload is not null)
        {
            var resumed = await CreateResumedOutputAsync(pendingUpload, cancellationToken);
            if (await SaveIdempotencyResultAsync(existingIdempotency, tenantId, actorAccountId, input, requestHash, resumed, now, cancellationToken))
            {
                return resumed;
            }

            return await RecoverConcurrentCreateAsync(tenantId, actorAccountId, input, requestHash, now, cancellationToken);
        }

        var expiredUpload = await videoUploads.GetExpiredPendingByFingerprintAsync(
            tenantId,
            actorAccountId,
            input.Fingerprint,
            now,
            cancellationToken);
        if (expiredUpload is not null)
        {
            await videoUploads.ExpireAsync(
                expiredUpload.UploadId,
                now,
                (upload, token) => mediaStorage.AbortMultipartUploadAsync(upload.ObjectKey, upload.StorageUploadId, token),
                cancellationToken);
        }

        var storageUploadId = await mediaStorage.InitiateMultipartUploadAsync(
            proposedUpload.ObjectKey,
            proposedUpload.ContentType,
            cancellationToken);
        proposedUpload.SetStorageUploadId(storageUploadId);
        await videoUploads.AddAsync(proposedUpload, cancellationToken);
        var created = new CreateVideoUploadOutput(VideoUploadUseCaseHelpers.ToOutput(proposedUpload, Array.Empty<int>()));
        if (await SaveIdempotencyResultAsync(existingIdempotency, tenantId, actorAccountId, input, requestHash, created, now, cancellationToken))
        {
            return created;
        }

        await AbortOrphanedUploadAsync(proposedUpload, cancellationToken);
        return await RecoverConcurrentCreateAsync(tenantId, actorAccountId, input, requestHash, now, cancellationToken);
    }

    private async Task<CreateVideoUploadOutput> RecoverConcurrentCreateAsync(
        Guid tenantId,
        Guid actorAccountId,
        CreateVideoUploadInput input,
        string requestHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var concurrentIdempotency = await idempotencyRecords.GetAsync(
            "createVideoUpload",
            tenantId,
            actorAccountId,
            input.IdempotencyKey,
            cancellationToken);
        if (concurrentIdempotency is not null && concurrentIdempotency.ExpiresAt > now)
        {
            VideoUploadUseCaseHelpers.EnsureSameRequest(concurrentIdempotency, requestHash);
            return VideoUploadUseCaseHelpers.Replay<CreateVideoUploadOutput>(concurrentIdempotency);
        }

        var concurrentUpload = await videoUploads.GetPendingByFingerprintAsync(
            tenantId,
            actorAccountId,
            input.Fingerprint,
            now,
            cancellationToken);
        if (concurrentUpload is null)
        {
            throw new InvalidOperationException("A concurrent unique conflict did not leave a matching video upload.");
        }

        var resumed = await CreateResumedOutputAsync(concurrentUpload, cancellationToken);
        if (await SaveIdempotencyResultAsync(concurrentIdempotency, tenantId, actorAccountId, input, requestHash, resumed, now, cancellationToken))
        {
            return resumed;
        }

        var replayedIdempotency = await idempotencyRecords.GetAsync(
            "createVideoUpload",
            tenantId,
            actorAccountId,
            input.IdempotencyKey,
            cancellationToken);
        if (replayedIdempotency is null || replayedIdempotency.ExpiresAt <= now)
        {
            throw new InvalidOperationException("The video upload idempotency conflict could not be reconciled.");
        }

        VideoUploadUseCaseHelpers.EnsureSameRequest(replayedIdempotency, requestHash);
        return VideoUploadUseCaseHelpers.Replay<CreateVideoUploadOutput>(replayedIdempotency);
    }

    private async Task<CreateVideoUploadOutput> CreateResumedOutputAsync(
        VideoUpload upload,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MediaUploadPart> parts;
        try
        {
            parts = await mediaStorage.ListPartsAsync(upload.ObjectKey, upload.StorageUploadId, cancellationToken);
        }
        catch (MultipartUploadNotFoundException)
        {
            throw VideoUploadUseCaseHelpers.UploadNotFound();
        }

        return new CreateVideoUploadOutput(
            VideoUploadUseCaseHelpers.ToOutput(upload, parts.Select(part => part.PartNumber).ToArray()),
            Resumed: true);
    }

    private async Task AbortOrphanedUploadAsync(VideoUpload upload, CancellationToken cancellationToken)
    {
        try
        {
            await mediaStorage.AbortMultipartUploadAsync(upload.ObjectKey, upload.StorageUploadId, cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // The persisted upload remains resumable; the orphaned multipart upload is reclaimed by storage lifecycle policy.
        }
    }

    private async Task<bool> SaveIdempotencyResultAsync(
        CodeForCoders.Media.Domain.Entities.OperationIdempotencyRecord? existing,
        Guid tenantId,
        Guid actorAccountId,
        CreateVideoUploadInput input,
        string requestHash,
        CreateVideoUploadOutput output,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var record = VideoUploadUseCaseHelpers.CreateOrReuseIdempotencyRecord(
            existing,
            tenantId,
            actorAccountId,
            "createVideoUpload",
            input.IdempotencyKey,
            requestHash,
            now);
        record.SetResponse(output.Resumed ? 200 : 201, System.Text.Json.JsonSerializer.Serialize(output), now.AddHours(24));
        if (existing is null)
        {
            await idempotencyRecords.AddAsync(record, cancellationToken);
        }

        var committed = await unitOfWork.TryCommitAsync(cancellationToken);
        if (committed && !output.Resumed)
        {
            MediaTelemetry.UploadsCreated.Add(1);
        }

        return committed;
    }
}
