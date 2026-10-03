namespace CodeForCoders.Identity.Application.UseCases.Accounts.LookupStudentAccount;

public sealed record LookupStudentAccountInput(Guid TenantId, Guid SessionId, string? Email);
