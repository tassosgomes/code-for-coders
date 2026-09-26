namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;

public sealed record ValidateStaffSessionInput(Guid TenantId, Guid SessionId);
