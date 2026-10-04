namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record StudentCourseProgress(Guid CourseId, int VersionNumber, int CompletedLessons, int TotalLessons,
    int Percent, IReadOnlyList<StudentLessonProgress> Lessons);
