namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.OpenPlaybackSession;

public sealed record OpenPlaybackSessionInput(Guid TenantId, Guid StudentId, Guid LessonId, string? Email);
