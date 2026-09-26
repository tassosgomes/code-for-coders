namespace CodeForCoders.Identity.Application.UseCases.Accounts.ListPendingStaffInvitations;

public sealed record ListPendingStaffInvitationsOutput(
    IReadOnlyList<PendingStaffInvitationOutput> Data,
    InvitationPaginationOutput Pagination);

public sealed record PendingStaffInvitationOutput(
    Guid InvitationId,
    string Email,
    string OfferedRole,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt);

public sealed record InvitationPaginationOutput(int Page, int Size, int Total, int TotalPages);
