namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record CatalogCourseSummary(Guid CourseId, string Title, string? Level, bool InShowcase, CatalogOfferCounts OfferCounts);
