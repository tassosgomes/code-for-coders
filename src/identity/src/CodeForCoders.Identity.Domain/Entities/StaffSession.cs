using CodeForCoders.Identity.Domain.SeedWork;

namespace CodeForCoders.Identity.Domain.Entities;

public sealed class StaffSession
{
    private StaffSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public DateTimeOffset CreatedOn { get; private set; }

    public DateTimeOffset ExpiresOn { get; private set; }

    public DateTimeOffset? RevokedOn { get; private set; }

    public static StaffSession Create(
        Guid id,
        Guid tenantId,
        Guid accountId,
        DateTimeOffset createdOn,
        DateTimeOffset expiresOn)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new EntityValidationException("Staff session identifiers are required.");
        }

        if (expiresOn <= createdOn)
        {
            throw new EntityValidationException("Staff session expiry must be after creation.");
        }

        return new StaffSession
        {
            Id = id,
            TenantId = tenantId,
            AccountId = accountId,
            CreatedOn = createdOn,
            ExpiresOn = expiresOn,
        };
    }

    public void Revoke(DateTimeOffset revokedOn)
    {
        if (RevokedOn is null)
        {
            RevokedOn = revokedOn;
        }
    }
}
