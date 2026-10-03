namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.RecordPlaybackProgress;

public sealed record RecordPlaybackProgressInput(
    Guid TenantId,
    Guid StudentId,
    Guid SessionId,
    int Sequence,
    int PositionSeconds,
    string Reason,
    string? TraceParent = null);
