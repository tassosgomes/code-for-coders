namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.OpenPlaybackSession;

public sealed record ProgressPolicyOutput(int IntervalSeconds, int MinGapSeconds);
