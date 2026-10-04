namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record StudentCourseLessonActivity(Guid CourseId, Guid LessonId, DateTimeOffset LastActivityAt, DateTimeOffset? CompletedAt);
