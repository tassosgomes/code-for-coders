namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseVersionModuleOutput(Guid ModuleId, string Title, int Position, IReadOnlyList<CourseVersionLessonOutput> Lessons);
