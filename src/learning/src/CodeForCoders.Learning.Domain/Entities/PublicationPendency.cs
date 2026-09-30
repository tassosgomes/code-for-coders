namespace CodeForCoders.Learning.Domain.Entities;

public sealed record PublicationPendency(string Code, Guid? ModuleId = null, Guid? LessonId = null);
