namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record FinanceOrderPage(IReadOnlyList<FinanceOrderSummary> Data, FinanceOrderPagination Pagination);
