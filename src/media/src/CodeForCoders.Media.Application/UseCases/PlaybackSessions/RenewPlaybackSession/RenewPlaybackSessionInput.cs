namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.RenewPlaybackSession;

public sealed record RenewPlaybackSessionInput(Guid StudentId, Guid SessionId, string? Email);
