namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record ShowcaseCoursePage(IReadOnlyList<ShowcaseCourseCard> Data, CatalogPagination Pagination);
