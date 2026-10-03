namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetPlaybackResource;

public sealed record GetPlaybackResourceInput(Guid StudentId, Guid SessionId, string Resource, string? Quality);
