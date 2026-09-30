namespace CodeForCoders.Learning.Application.UseCases.Courses.ListCourseVersions;

public sealed record ListCourseVersionsInput(Guid CourseId, int Page, int Size);
