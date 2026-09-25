namespace CodeForCoders.Identity.Application.UseCases.Accounts.ProvisionFirstAdministrator;

public enum ProvisionFirstAdministratorStatus
{
    Created,
    AlreadyProvisioned,
    EmailBelongsToStudent,
    EmailAlreadyInUse,
}
