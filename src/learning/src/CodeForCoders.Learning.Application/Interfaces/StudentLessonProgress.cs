namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record StudentLessonProgress(Guid LessonId, bool Completed, int? LastPositionSeconds, int ResumeAtSeconds);
