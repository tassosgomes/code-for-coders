using CodeForCoders.BffAdmin.Api.ApiModels;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface ICommerceFinanceAreaClient
{
    Task<CommerceFinanceAreaResult> GetFinanceAreaAsync(string accessToken, CancellationToken cancellationToken);
}

public sealed record CommerceFinanceAreaResult(int StatusCode, string? Code, FinanceAreaResponse? Area);
