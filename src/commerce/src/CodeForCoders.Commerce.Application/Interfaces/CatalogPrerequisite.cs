namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record CatalogPrerequisite(string? Text, IReadOnlyList<CatalogRecommendedCourse> RecommendedCourses);
