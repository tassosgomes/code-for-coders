using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record StaffMemberIdentityResult(
    int StatusCode,
    string? Code,
    StaffMemberPageV1? Page = null,
    StaffRoleActionResultV1? Action = null);
