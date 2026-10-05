namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record CurrentStudentCourse(Guid CourseId, string Title, IReadOnlyList<Guid> LessonIds);
