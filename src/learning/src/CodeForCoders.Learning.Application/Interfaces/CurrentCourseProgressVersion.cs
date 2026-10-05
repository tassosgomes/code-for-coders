namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record CurrentCourseProgressVersion(Guid CourseId, int VersionNumber, IReadOnlyList<CurrentProgressLesson> Lessons);
