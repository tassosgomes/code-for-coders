namespace CodeForCoders.Identity.Application.UseCases.Accounts.AcceptStaffInvitation;

public sealed record AcceptStaffInvitationOutput(
    Guid SessionId,
    Guid AccountId,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset ExpiresAt);
