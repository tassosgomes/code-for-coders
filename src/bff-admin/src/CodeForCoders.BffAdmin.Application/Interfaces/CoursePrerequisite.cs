namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CoursePrerequisite(string? Text, IReadOnlyList<RecommendedCourse> RecommendedCourses);
