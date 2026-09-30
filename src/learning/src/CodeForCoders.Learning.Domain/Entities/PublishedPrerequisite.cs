namespace CodeForCoders.Learning.Domain.Entities;

public sealed record PublishedPrerequisite(string? Text, IReadOnlyList<PublishedRecommendedCourse> RecommendedCourses);
