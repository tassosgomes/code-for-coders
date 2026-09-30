namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseModuleOutput(Guid ModuleId, string Title, int Position, IReadOnlyList<CourseLessonOutput> Lessons);
