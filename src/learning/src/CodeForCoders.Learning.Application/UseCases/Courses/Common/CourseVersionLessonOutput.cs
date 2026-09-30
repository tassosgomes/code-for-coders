namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseVersionLessonOutput(Guid LessonId, string Title, string? Description, int Position, Guid VideoId);
