using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record StaffSessionIdentityCreatedResult(
    int StatusCode,
    string? Code,
    StaffSessionCreatedV1? Session);

public sealed record StaffSessionIdentityValidatedResult(
    int StatusCode,
    string? Code,
    StaffSessionValidatedV1? Session);

public sealed record StaffSessionIdentityRevokedResult(int StatusCode, string? Code);
