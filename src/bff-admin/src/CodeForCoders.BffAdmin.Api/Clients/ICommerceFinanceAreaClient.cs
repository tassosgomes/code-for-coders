using CodeForCoders.BffAdmin.Api.ApiModels;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface ICommerceFinanceAreaClient
{
    Task<FinanceOrdersResult<CommerceFinanceOrderPage>> ListOrdersAsync(string accessToken, FinanceOrderFilters filters, CancellationToken cancellationToken);
    Task<FinanceOrdersResult<CommerceFinanceOrderDetail>> GetOrderAsync(string accessToken, Guid orderId, CancellationToken cancellationToken);
    Task<CommerceFinanceAreaResult> GetFinanceAreaAsync(string accessToken, CancellationToken cancellationToken);
}

public sealed record CommerceFinanceAreaResult(int StatusCode, string? Code, FinanceAreaResponse? Area);
