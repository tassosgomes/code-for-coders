namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record CatalogCoursePage(IReadOnlyList<CatalogCourseSummary> Data, CatalogPagination Pagination);
