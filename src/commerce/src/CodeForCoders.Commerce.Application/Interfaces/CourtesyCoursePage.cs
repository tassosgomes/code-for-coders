namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record CourtesyCoursePage(IReadOnlyList<CourtesyCourse> Data, CatalogPagination Pagination);
