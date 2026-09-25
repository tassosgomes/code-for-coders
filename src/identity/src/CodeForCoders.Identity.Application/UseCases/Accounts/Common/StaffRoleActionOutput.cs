namespace CodeForCoders.Identity.Application.UseCases.Accounts.Common;

public sealed record StaffRoleActionOutput(
    StaffMemberOutput Member,
    bool Changed,
    bool SessionsEnded);
