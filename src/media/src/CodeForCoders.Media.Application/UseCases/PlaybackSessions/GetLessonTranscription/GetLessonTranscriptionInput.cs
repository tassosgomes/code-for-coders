namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetLessonTranscription;

public sealed record GetLessonTranscriptionInput(Guid TenantId, Guid StudentId, Guid LessonId);
