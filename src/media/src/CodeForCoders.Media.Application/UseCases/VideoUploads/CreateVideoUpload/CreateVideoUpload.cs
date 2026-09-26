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
            actorAccountId,
            input.IdempotencyKey,
            cancellationToken);
        if (existingIdempotency is not null && existingIdempotency.ExpiresAt > now)
        {
            VideoUploadUseCaseHelpers.EnsureSameRequest(existingIdempotency, requestHash);
            return VideoUploadUseCaseHelpers.Replay<CreateVideoUploadOutput>(existingIdempotency);
        }

        var storageUploadId = await mediaStorage.InitiateMultipartUploadAsync(
            proposedUpload.ObjectKey,
            proposedUpload.ContentType,
            cancellationToken);
        proposedUpload.SetStorageUploadId(storageUploadId);
        await videoUploads.AddAsync(proposedUpload, cancellationToken);
        var result = new CreateVideoUploadOutput(VideoUploadUseCaseHelpers.ToOutput(proposedUpload, Array.Empty<int>()));
        await SaveIdempotencyResultAsync(existingIdempotency, tenantId, actorAccountId, input, requestHash, result, now, cancellationToken);

        return result;
    }

    private async Task SaveIdempotencyResultAsync(
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
        record.SetResponse(201, System.Text.Json.JsonSerializer.Serialize(output), now.AddHours(24));
        if (existing is null)
        {
            await idempotencyRecords.AddAsync(record, cancellationToken);
        }

        await unitOfWork.CommitAsync(cancellationToken);
    }
}
