namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.Common;

public sealed record ProgressPolicyOutput(int IntervalSeconds, int MinGapSeconds);
