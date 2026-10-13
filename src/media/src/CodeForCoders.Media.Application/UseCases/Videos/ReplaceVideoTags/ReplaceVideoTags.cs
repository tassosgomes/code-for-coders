using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.VideoUploads;
using CodeForCoders.Media.Domain.ValueObjects;

namespace CodeForCoders.Media.Application.UseCases.Videos.ReplaceVideoTags;

public sealed class ReplaceVideoTags(
    ITenantContext tenantContext,
    IVideoTagRepository videoTags,
    IVideoTranscriptionRepository transcriptions,
    IOperationIdempotencyRepository idempotencyRecords,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IReplaceVideoTags
{
    public async Task<VideoOutput> ExecuteAsync(ReplaceVideoTagsInput input, CancellationToken cancellationToken)
    {
        var (tenantId, actorAccountId) = VideoUploadUseCaseHelpers.RequireActor(tenantContext);
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128
            || input.Tags is null || input.Tags.Count > 50 || input.Tags.Any(tag => tag is null || tag.Length > 100))
            throw new MediaApiException(400, "INVALID_REQUEST", "The video tags request is invalid.");
        var tags = VideoTagSet.Normalize(input.Tags);
        var now = timeProvider.GetUtcNow();
        var requestHash = VideoUploadUseCaseHelpers.HashRequest(new { input.VideoId, input.Tags });
        VideoOutput? output = null;
        var found = await videoTags.WithLockedVideoAsync(new(input.VideoId, actorAccountId, input.IdempotencyKey), async (video, token) =>
        {
            var existing = await idempotencyRecords.GetAsync("replaceVideoTags", tenantId, actorAccountId, input.IdempotencyKey, token);
            if (existing is not null && existing.ExpiresAt > now)
            {
                VideoUploadUseCaseHelpers.EnsureSameRequest(existing, requestHash);
                output = VideoUploadUseCaseHelpers.Replay<VideoOutput>(existing);
                return;
            }
            var canonical = await videoTags.ReplaceAsync(video.VideoId, tags, token);
            var states = await transcriptions.GetAsync([video], token);
            output = new VideoOutput(video.VideoId, video.Title, video.Status,
                new VideoUploaderOutput(video.UploadedByAccountId, video.UploadedByName), video.UploadedAt,
                video.DurationSeconds, video.FailureReason, canonical, VideoTranscriptionOutput.FromState(states[video.VideoId]));
            var record = VideoUploadUseCaseHelpers.CreateOrReuseIdempotencyRecord(existing, tenantId, actorAccountId,
                "replaceVideoTags", input.IdempotencyKey, requestHash, now);
            record.SetResponse(200, JsonSerializer.Serialize(output), now.AddHours(24));
            if (existing is null) await idempotencyRecords.AddAsync(record, token);
            await unitOfWork.CommitAsync(token);
        }, cancellationToken);
        if (!found) throw new MediaApiException(404, "VIDEO_NOT_FOUND", "The requested video was not found.");
        return output!;
    }
}
