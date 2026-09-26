namespace CodeForCoders.Identity.Application.UseCases.Accounts.GrantStaffRole;

public sealed record GrantStaffRoleInput(
    Guid TenantId,
    Guid ActorAccountId,
    Guid TargetAccountId,
    string Role,
    string Reason,
    string IdempotencyKey);
