namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record FinanceOrderPage(IReadOnlyList<FinanceOrderSummary> Data, CatalogPagination Pagination);
