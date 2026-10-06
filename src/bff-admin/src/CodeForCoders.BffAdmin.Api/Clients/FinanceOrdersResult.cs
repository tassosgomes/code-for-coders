namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record FinanceOrdersResult<T>(int StatusCode, string? Code, T? Value);
