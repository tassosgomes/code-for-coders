namespace CodeForCoders.Learning.Domain.Entities;

public sealed record PublishedLesson(Guid LessonId, string Title, string? Description, int Position, Guid VideoId);
