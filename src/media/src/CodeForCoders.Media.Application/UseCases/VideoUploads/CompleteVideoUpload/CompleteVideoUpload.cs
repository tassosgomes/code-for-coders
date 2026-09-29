using System.Diagnostics;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.VideoUploads;
using CodeForCoders.Media.Application.UseCases.Videos;
using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.CompleteVideoUpload;

public sealed class CompleteVideoUpload(
    ITenantContext tenantContext,
    IVideoUploadRepository videoUploads,
    IOperationIdempotencyRepository idempotencyRecords,
    IMediaStoragePort mediaStorage,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICompleteVideoUpload
{
    public async Task<CompletedVideoOutput> ExecuteAsync(
        CompleteVideoUploadInput input,
        CancellationToken cancellationToken)
    {
        var (tenantId, actorAccountId) = VideoUploadUseCaseHelpers.RequireActor(tenantContext);
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128)
        {
            throw new MediaApiException(400, "INVALID_REQUEST", "A valid idempotency key is required.");
        }

        var requestHash = VideoUploadUseCaseHelpers.HashRequest(new { input.UploadId });
        var existingIdempotency = await idempotencyRecords.GetAsync(
            "completeVideoUpload",
            tenantId,
            actorAccountId,
            input.IdempotencyKey,
            cancellationToken);
        if (existingIdempotency is not null && existingIdempotency.ExpiresAt > now)
        {
            VideoUploadUseCaseHelpers.EnsureSameRequest(existingIdempotency, requestHash);
            return VideoUploadUseCaseHelpers.Replay<CompletedVideoOutput>(existingIdempotency);
        }

        var upload = await videoUploads.GetOwnedAsync(input.UploadId, tenantId, actorAccountId, true, cancellationToken);
        if (upload is null || (!upload.IsCompleted && upload.ExpiresAt <= now))
        {
            throw VideoUploadUseCaseHelpers.UploadNotFound();
        }

        var video = Video.Create(new VideoCreateInput(
            upload.VideoId,
            upload.TenantId,
            upload.Title,
            upload.UploaderAccountId,
            upload.UploaderName,
            now,
            upload.ObjectKey,
            upload.FileSize,
            Activity.Current?.Id));
        var (completedVideo, newlyCompleted) = await videoUploads.CompleteAsync(
            upload,
            video,
            now,
            CompleteStorageAsync,
            cancellationToken);
        if (completedVideo is null)
        {
            throw VideoUploadUseCaseHelpers.UploadNotFound();
        }

        var output = new CompletedVideoOutput(ToOutput(completedVideo));
        var idempotencyRecord = VideoUploadUseCaseHelpers.CreateOrReuseIdempotencyRecord(
            existingIdempotency,
            upload.TenantId,
            actorAccountId,
            "completeVideoUpload",
            input.IdempotencyKey,
            requestHash,
            now);
        idempotencyRecord.SetResponse(201, System.Text.Json.JsonSerializer.Serialize(output), now.AddHours(24));
        if (existingIdempotency is null)
        {
            await idempotencyRecords.AddAsync(idempotencyRecord, cancellationToken);
        }

        await unitOfWork.CommitAsync(cancellationToken);
        if (newlyCompleted)
        {
            MediaTelemetry.UploadsCompleted.Add(1);
            MediaTelemetry.UploadSize.Record(upload.FileSize);
        }

        return output;
    }

    private async Task CompleteStorageAsync(VideoUpload upload, CancellationToken cancellationToken)
    {
        var parts = await mediaStorage.ListPartsAsync(upload.ObjectKey, upload.StorageUploadId, cancellationToken);
        if (!HasExpectedParts(upload, parts))
        {
            var received = parts.Count(part => part.PartNumber >= 1 && part.PartNumber <= upload.PartCount);
            var missing = Math.Max(upload.PartCount - received, 0);
            throw new MediaApiException(
                422,
                "UPLOAD_INCOMPLETE",
                "The upload is missing or has incomplete parts.",
                $"Missing or invalid parts: {missing} of {upload.PartCount}.");
        }

        await mediaStorage.CompleteMultipartUploadAsync(
            upload.ObjectKey,
            upload.StorageUploadId,
            parts,
            cancellationToken);
    }

    private static bool HasExpectedParts(VideoUpload upload, IReadOnlyList<MediaUploadPart> parts)
    {
        if (parts.Count != upload.PartCount)
        {
            return false;
        }

        for (var partNumber = 1; partNumber <= upload.PartCount; partNumber++)
        {
            var part = parts.FirstOrDefault(candidate => candidate.PartNumber == partNumber);
            var expectedSize = partNumber == upload.PartCount
                ? upload.FileSize - (VideoUpload.PartSizeBytes * (upload.PartCount - 1))
                : VideoUpload.PartSizeBytes;
            if (part is null || part.Size != expectedSize)
            {
                return false;
            }
        }

        return true;
    }

    private static VideoOutput ToOutput(Video video)
        => new(
            video.VideoId,
            video.Title,
            video.Status,
            new VideoUploaderOutput(video.UploadedByAccountId, video.UploadedByName),
            video.UploadedAt,
            video.DurationSeconds,
            video.FailureReason);
}
