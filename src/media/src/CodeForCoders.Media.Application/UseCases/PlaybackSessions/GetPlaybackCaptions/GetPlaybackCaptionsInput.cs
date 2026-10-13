namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetPlaybackCaptions;

public sealed record GetPlaybackCaptionsInput(Guid TenantId, Guid StudentId, Guid SessionId);
