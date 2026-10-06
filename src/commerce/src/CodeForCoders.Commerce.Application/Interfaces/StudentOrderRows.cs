namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentOrderRows(IReadOnlyList<StudentOrderSnapshot> Data, CatalogPagination Pagination);
