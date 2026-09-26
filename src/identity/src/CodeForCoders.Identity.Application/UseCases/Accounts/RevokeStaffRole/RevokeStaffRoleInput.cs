namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffRole;

public sealed record RevokeStaffRoleInput(
    Guid TenantId,
    Guid ActorAccountId,
    Guid TargetAccountId,
    string Role,
    string Reason,
    string IdempotencyKey);
