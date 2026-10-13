using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.Transcriptions.Common;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetLessonTranscription;

public sealed class GetLessonTranscription(IPlaybackRepository lessons, IAccessDecisionClient decisions,
    ITranscriptionQueries transcriptions, IOptions<TranscriptionOptions> options) : IGetLessonTranscription
{
    public async Task<LessonTranscriptionOutput> ExecuteAsync(GetLessonTranscriptionInput input, CancellationToken cancellationToken)
    {
        var lesson = await lessons.FindLessonAsync(input.LessonId, cancellationToken);
        if (lesson is null)
            throw new MediaApiException(404, "LESSON_NOT_AVAILABLE", "This lesson is not available.");
        var decision = await decisions.DecideAsync(new(input.TenantId, input.StudentId, lesson.CourseId), cancellationToken);
        if (decision is null)
            throw new MediaApiException(503, "ACCESS_DECISION_UNAVAILABLE", "Access could not be verified.");
        if (decision.Decision != "allowed")
            throw new MediaApiException(403, "ACCESS_DENIED", "Access to this lesson is denied.")
            { Reason = decision.DeniedReason, AccessEndedAt = decision.LastExpiredAt };
        var current = options.Value.Enabled && lesson.Status == "ready"
            ? await transcriptions.GetCurrentAsync(lesson.VideoId, cancellationToken) : null;
        return current is null
            ? new(input.LessonId, "unavailable", null, null, null, [])
            : new(input.LessonId, "ready", "tr_" + current.TranscriptionId.ToString("N")[^8..], current.Language,
                current.DurationMs, JsonSerializer.Deserialize<TranscriptSegmentOutput[]>(current.SegmentsJson, JsonSerializerOptions.Web)!);
    }
}
