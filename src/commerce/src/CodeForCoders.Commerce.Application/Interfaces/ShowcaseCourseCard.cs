namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record ShowcaseCourseCard(
    Guid CourseId,
    string Title,
    string Level,
    string Summary,
    int LowestPriceCents,
    int OfferCount);
