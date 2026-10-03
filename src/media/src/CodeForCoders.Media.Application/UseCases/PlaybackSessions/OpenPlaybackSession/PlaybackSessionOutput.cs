using CodeForCoders.Media.Application.Interfaces;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.OpenPlaybackSession;

public sealed record PlaybackSessionOutput(Guid SessionId, Guid LessonId, DateTimeOffset ExpiresAt,
    DateTimeOffset RenewAfter, WatermarkOutput Watermark, ProgressPolicyOutput Progress, SegmentAccess SegmentAccess);
