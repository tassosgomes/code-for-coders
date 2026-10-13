using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.VideoUploads;
using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.UseCases.Videos.RequestVideoTranscription;

public sealed class RequestVideoTranscription(
    ITenantContext tenantContext,
    IVideoTranscriptionRepository transcriptions,
    IVideoTagRepository videoTags,
    IOperationIdempotencyRepository idempotencyRecords,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestVideoTranscription
{
    public async Task<VideoOutput> ExecuteAsync(RequestVideoTranscriptionInput input, CancellationToken cancellationToken)
    {
        var (tenantId, actorAccountId) = VideoUploadUseCaseHelpers.RequireActor(tenantContext);
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128)
            throw new MediaApiException(400, "INVALID_REQUEST", "A valid idempotency key is required.");
        var now = timeProvider.GetUtcNow();
        var hash = VideoUploadUseCaseHelpers.HashRequest(new { input.VideoId });
        VideoOutput? output = null;
        var enqueued = false;
        var found = await transcriptions.WithRequestAsync(new(input.VideoId, actorAccountId, input.IdempotencyKey), async (video, token) =>
        {
            var existing = await idempotencyRecords.GetAsync("requestVideoTranscription", tenantId, actorAccountId, input.IdempotencyKey, token);
            if (existing is not null && existing.ExpiresAt > now)
            {
                VideoUploadUseCaseHelpers.EnsureSameRequest(existing, hash);
                output = VideoUploadUseCaseHelpers.Replay<VideoOutput>(existing);
                return;
            }
            if (video.Status != "ready") throw new MediaApiException(409, "VIDEO_NOT_READY", "Only a ready video can be transcribed.");
            var states = await transcriptions.GetAsync([video], token);
            if (states[video.VideoId].Status != "in-progress" && !states[video.VideoId].CanStart)
                throw new MediaApiException(409, "TRANSCRIPTION_NOT_RETRYABLE", "This video cannot be transcribed again.");
            enqueued = await transcriptions.EnqueueAsync(Transcription.Create(new(tenantId, video.VideoId, "requested", actorAccountId, now)), token);
            states = await transcriptions.GetAsync([video], token);
            var tags = await videoTags.GetAsync([video.VideoId], token);
            output = new(video.VideoId, video.Title, video.Status,
                new(video.UploadedByAccountId, video.UploadedByName), video.UploadedAt, video.DurationSeconds,
                video.FailureReason, tags.GetValueOrDefault(video.VideoId, []), VideoTranscriptionOutput.FromState(states[video.VideoId]));
            var record = VideoUploadUseCaseHelpers.CreateOrReuseIdempotencyRecord(existing, tenantId, actorAccountId,
                "requestVideoTranscription", input.IdempotencyKey, hash, now);
            record.SetResponse(202, JsonSerializer.Serialize(output), now.AddHours(24));
            if (existing is null) await idempotencyRecords.AddAsync(record, token);
            await unitOfWork.CommitAsync(token);
        }, cancellationToken);
        if (!found) throw new MediaApiException(404, "VIDEO_NOT_FOUND", "The requested video was not found.");
        if (enqueued) MediaTelemetry.RecordTranscriptionRequested("requested");
        return output!;
    }
}
