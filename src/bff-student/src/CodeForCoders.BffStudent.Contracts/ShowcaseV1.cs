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
