namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record StudentModuleOutline(Guid ModuleId, string Title, int Position, IReadOnlyList<StudentLessonOutline> Lessons);
