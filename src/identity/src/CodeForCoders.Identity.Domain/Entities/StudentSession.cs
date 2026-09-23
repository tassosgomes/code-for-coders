using CodeForCoders.Identity.Domain.SeedWork;

namespace CodeForCoders.Identity.Domain.Entities;

public sealed class StudentSession
{
    private StudentSession()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public DateTimeOffset CreatedOn { get; private set; }

    public DateTimeOffset LastActivityOn { get; private set; }

    public DateTimeOffset ExpiresOn { get; private set; }

    public DateTimeOffset? RevokedOn { get; private set; }

    public static StudentSession Create(
        Guid id,
        Guid tenantId,
        Guid accountId,
        DateTimeOffset now,
        DateTimeOffset expiresOn)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new EntityValidationException("Student session identifiers are required.");
        }

        if (expiresOn <= now)
        {
            throw new EntityValidationException("Student session expiration must be in the future.");
        }

        return new StudentSession
        {
            Id = id,
            TenantId = tenantId,
            AccountId = accountId,
            CreatedOn = now,
            LastActivityOn = now,
            ExpiresOn = expiresOn,
        };
    }

    public void Revoke(DateTimeOffset revokedOn)
        => RevokedOn ??= revokedOn;
}
