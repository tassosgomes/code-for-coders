using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record StudentAccountLookupResult(int StatusCode, string? Code, StudentAccountV1? Account);
