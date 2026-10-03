namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentAccessGrantPage(IReadOnlyList<StudentAccessGrant> Data, CatalogPagination Pagination);
