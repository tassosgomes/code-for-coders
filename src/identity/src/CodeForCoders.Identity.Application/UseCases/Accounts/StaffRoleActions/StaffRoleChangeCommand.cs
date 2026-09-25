namespace CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;

public sealed record StaffRoleChangeCommand(
    Guid TenantId,
    Guid ActorAccountId,
    Guid TargetAccountId,
    string FromRole,
    string ToRole,
    string Reason,
    string IdempotencyKey);
