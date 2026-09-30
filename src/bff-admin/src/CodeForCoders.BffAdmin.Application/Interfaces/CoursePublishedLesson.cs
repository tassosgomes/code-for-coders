namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CoursePublishedLesson(Guid LessonId, string Title, string? Description, int Position, Guid VideoId);
