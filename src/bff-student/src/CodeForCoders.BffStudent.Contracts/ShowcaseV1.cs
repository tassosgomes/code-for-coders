namespace CodeForCoders.BffStudent.Contracts;

public sealed record ShowcaseCourseCardV1(
    Guid CourseId,
    string Title,
    string Level,
    string Summary,
    int LowestPriceCents,
    int OfferCount);

public sealed record ShowcasePaginationV1(int Page, int Size, int Total, int TotalPages);

public sealed record ShowcaseCoursePageV1(IReadOnlyList<ShowcaseCourseCardV1> Data, ShowcasePaginationV1 Pagination);

public sealed record ShowcaseCourseDetailV1(
    Guid CourseId,
    string Title,
    string Level,
    string Description,
    ShowcasePrerequisiteV1 Prerequisite,
    IReadOnlyList<ShowcaseModuleV1> Modules,
    IReadOnlyList<ShowcaseOfferV1> Offers);

public sealed record ShowcasePrerequisiteV1(string? Text, IReadOnlyList<ShowcaseRecommendedCourseV1> RecommendedCourses);

public sealed record ShowcaseRecommendedCourseV1(Guid CourseId, string Title, bool InShowcase);

public sealed record ShowcaseModuleV1(string Title, IReadOnlyList<ShowcaseLessonV1> Lessons);

public sealed record ShowcaseLessonV1(string Title);

public sealed record ShowcaseOfferV1(Guid OfferId, string Name, int PriceCents, ShowcaseAccessPeriodV1 AccessPeriod);

public sealed record ShowcaseAccessPeriodV1(
    string Type,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Months);
