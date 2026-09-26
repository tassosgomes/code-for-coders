namespace CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;

public sealed record StaffRoleActionCommand(
    Guid TenantId,
    Guid ActorAccountId,
    Guid TargetAccountId,
    string Role,
    string Reason,
    string IdempotencyKey);
