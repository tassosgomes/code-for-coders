namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CoursePrerequisiteOutput(string? Text, IReadOnlyList<RecommendedCourseOutput> RecommendedCourses);
