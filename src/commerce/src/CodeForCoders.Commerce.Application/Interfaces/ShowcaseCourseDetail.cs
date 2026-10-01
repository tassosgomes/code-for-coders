using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record ShowcaseCourseDetail(
    Guid CourseId,
    string Title,
    string Level,
    string Description,
    ShowcasePrerequisite Prerequisite,
    IReadOnlyList<ShowcaseModule> Modules,
    IReadOnlyList<ShowcaseOffer> Offers);

public sealed record ShowcasePrerequisite(string? Text, IReadOnlyList<ShowcaseRecommendedCourse> RecommendedCourses);

public sealed record ShowcaseRecommendedCourse(Guid CourseId, string Title, bool InShowcase);

public sealed record ShowcaseModule(string Title, IReadOnlyList<ShowcaseLesson> Lessons);

public sealed record ShowcaseLesson(string Title);

public sealed record ShowcaseOffer(Guid OfferId, string Name, int PriceCents, AccessPeriod AccessPeriod);
