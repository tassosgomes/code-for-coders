namespace CodeForCoders.Identity.Application.UseCases.Accounts.ProvisionFirstAdministrator;

public sealed record ProvisionFirstAdministratorInput(Guid TenantId, string Email, string Name);
