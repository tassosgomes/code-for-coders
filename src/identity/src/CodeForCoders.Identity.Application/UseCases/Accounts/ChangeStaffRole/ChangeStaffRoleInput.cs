namespace CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStaffRole;

public sealed record ChangeStaffRoleInput(
    Guid TenantId,
    Guid ActorAccountId,
    Guid TargetAccountId,
    string FromRole,
    string ToRole,
    string Reason,
    string IdempotencyKey);
