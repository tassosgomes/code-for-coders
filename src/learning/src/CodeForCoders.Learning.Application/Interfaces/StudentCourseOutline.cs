namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record StudentCourseOutline(Guid CourseId, string Title, int VersionNumber, IReadOnlyList<StudentModuleOutline> Modules);
