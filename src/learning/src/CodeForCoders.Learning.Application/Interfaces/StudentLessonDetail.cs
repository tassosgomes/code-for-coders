namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record StudentLessonDetail(Guid LessonId, Guid ModuleId, string Title, int Position);
