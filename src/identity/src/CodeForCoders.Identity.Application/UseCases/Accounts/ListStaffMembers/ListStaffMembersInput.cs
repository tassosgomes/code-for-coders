namespace CodeForCoders.Identity.Application.UseCases.Accounts.ListStaffMembers;

public sealed record ListStaffMembersInput(Guid TenantId, Guid ActorAccountId, int Page, int Size);
