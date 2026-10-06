namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record OrderProxyRequest(HttpMethod Method, string Path, string AccessToken, Guid? OfferId = null, string? IdempotencyKey = null);
