using CodeForCoders.BffAdmin.Api.ApiModels;
namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record CommerceFinanceOrderPage(IReadOnlyList<CommerceFinanceOrderSummary> Data, FinanceOrderPagination Pagination);
