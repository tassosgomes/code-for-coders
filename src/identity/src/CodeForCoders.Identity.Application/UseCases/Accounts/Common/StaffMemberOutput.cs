namespace CodeForCoders.Identity.Application.UseCases.Accounts.Common;

public sealed record StaffMemberOutput(
    Guid AccountId,
    string Name,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsSelf);
