namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record StaffSessionResponse(
    Guid AccountId,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string CsrfToken);
