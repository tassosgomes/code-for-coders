namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveStudentAccounts;

public sealed record ResolveStudentAccountsInput(Guid TenantId, Guid SessionId, IReadOnlyList<Guid>? StudentIds);
