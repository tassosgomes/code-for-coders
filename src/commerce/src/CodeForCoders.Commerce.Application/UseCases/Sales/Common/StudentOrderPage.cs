using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.Sales.Common;

public sealed record StudentOrderPage(IReadOnlyList<StudentOrder> Data, CatalogPagination Pagination);
