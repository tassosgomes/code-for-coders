namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffSession;

public sealed record RevokeStaffSessionInput(Guid TenantId, Guid SessionId, string IdempotencyKey);
