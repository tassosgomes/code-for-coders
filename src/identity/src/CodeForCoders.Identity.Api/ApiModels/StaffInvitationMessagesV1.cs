namespace CodeForCoders.Identity.Api.ApiModels;

public sealed record CreateStaffInvitationRequestV1(string? Email, string? Role, string? Reason);

public sealed record StaffInvitationCreatedV1(
    Guid InvitationId,
    string Email,
    string OfferedRole,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    Guid? SupersededInvitationId);

public sealed record PendingStaffInvitationV1(
    Guid InvitationId,
    string Email,
    string OfferedRole,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt);

public sealed record InvitationPaginationV1(int Page, int Size, int Total, int TotalPages);

public sealed record StaffInvitationPageV1(
    IReadOnlyList<PendingStaffInvitationV1> Data,
    InvitationPaginationV1 Pagination);
