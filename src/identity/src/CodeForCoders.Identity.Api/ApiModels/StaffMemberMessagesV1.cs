namespace CodeForCoders.Identity.Api.ApiModels;

public sealed record StaffMemberRoleActionRequestV1(string? Role, string? Reason);

public sealed record StaffMemberRoleChangeRequestV1(string? FromRole, string? ToRole, string? Reason);

public sealed record StaffMemberV1(
    Guid AccountId,
    string Name,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsSelf);

public sealed record StaffMemberPaginationV1(int Page, int Size, int Total, int TotalPages);

public sealed record StaffMemberPageV1(
    IReadOnlyList<StaffMemberV1> Data,
    StaffMemberPaginationV1 Pagination);

public sealed record StaffRoleActionResultV1(
    StaffMemberV1 Member,
    bool Changed,
    bool SessionsEnded);
