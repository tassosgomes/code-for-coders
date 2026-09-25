using CodeForCoders.Identity.Domain.SeedWork;

namespace CodeForCoders.Identity.Domain.Entities;

public sealed class StaffInvitation
{
    private StaffInvitation()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string OfferedRole { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset InvitedOn { get; private set; }

    public DateTimeOffset ExpiresOn { get; private set; }

    public DateTimeOffset? AcceptedOn { get; private set; }

    public DateTimeOffset? SupersededOn { get; private set; }

    public static StaffInvitation Create(
        Guid id,
        Guid tenantId,
        string email,
        string normalizedEmail,
        string offeredRole,
        string tokenHash,
        DateTimeOffset invitedOn,
        DateTimeOffset expiresOn)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new EntityValidationException("Staff invitation identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new EntityValidationException("Staff invitation email is required.");
        }

        if (!StaffRoleCatalog.Contains(offeredRole))
        {
            throw new EntityValidationException("The offered staff role is not in the catalog.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash) || expiresOn <= invitedOn)
        {
            throw new EntityValidationException("Staff invitation token and validity are required.");
        }

        return new StaffInvitation
        {
            Id = id,
            TenantId = tenantId,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            OfferedRole = offeredRole,
            TokenHash = tokenHash,
            InvitedOn = invitedOn,
            ExpiresOn = expiresOn,
        };
    }

    public void Supersede(DateTimeOffset supersededOn)
    {
        if (AcceptedOn is not null || SupersededOn is not null)
        {
            throw new EntityValidationException("Only an active staff invitation can be superseded.");
        }

        SupersededOn = supersededOn;
    }
}
