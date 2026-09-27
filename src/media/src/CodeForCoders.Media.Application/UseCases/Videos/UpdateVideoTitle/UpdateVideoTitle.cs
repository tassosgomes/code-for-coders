using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.VideoUploads;

namespace CodeForCoders.Media.Application.UseCases.Videos.UpdateVideoTitle;

public sealed class UpdateVideoTitle(
    ITenantContext tenantContext,
    IVideoQueries videoQueries,
    IOperationIdempotencyRepository idempotencyRecords,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUpdateVideoTitle
{
    public async Task<VideoOutput> ExecuteAsync(UpdateVideoTitleInput input, CancellationToken cancellationToken)
    {
        var (tenantId, actorAccountId) = VideoUploadUseCaseHelpers.RequireActor(tenantContext);
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128)
        {
            throw new MediaApiException(400, "INVALID_REQUEST", "A valid idempotency key is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200)
        {
            throw new MediaApiException(422, "TITLE_REQUIRED", "A video title is required.");
        }

        var now = timeProvider.GetUtcNow();
        var requestHash = VideoUploadUseCaseHelpers.HashRequest(new { input.VideoId, input.Title });
        var existing = await idempotencyRecords.GetAsync(
            "updateVideoTitle", tenantId, actorAccountId, input.IdempotencyKey, cancellationToken);
        if (existing is not null && existing.ExpiresAt > now)
        {
            VideoUploadUseCaseHelpers.EnsureSameRequest(existing, requestHash);
            return VideoUploadUseCaseHelpers.Replay<VideoOutput>(existing);
        }

        var video = await videoQueries.GetForUpdateAsync(input.VideoId, cancellationToken)
            ?? throw new MediaApiException(404, "VIDEO_NOT_FOUND", "The requested video was not found.");
        video.UpdateTitle(input.Title);
        var output = new VideoOutput(
            video.VideoId,
            video.Title,
            video.Status,
            new VideoUploaderOutput(video.UploadedByAccountId, video.UploadedByName),
            video.UploadedAt,
            video.DurationSeconds,
            video.FailureReason);
        var record = VideoUploadUseCaseHelpers.CreateOrReuseIdempotencyRecord(
            existing, tenantId, actorAccountId, "updateVideoTitle", input.IdempotencyKey, requestHash, now);
        record.SetResponse(200, JsonSerializer.Serialize(output), now.AddHours(24));
        if (existing is null)
        {
            await idempotencyRecords.AddAsync(record, cancellationToken);
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return output;
    }
}
