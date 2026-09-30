namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CoursePublishedModule(Guid ModuleId, string Title, int Position, IReadOnlyList<CoursePublishedLesson> Lessons);
