using CodeForCoders.Identity.Domain.SeedWork;

namespace CodeForCoders.Identity.Domain.Entities;

public sealed class VerificationToken
{
    private VerificationToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public string Purpose { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresOn { get; private set; }

    public DateTimeOffset? ConsumedOn { get; private set; }

    public static VerificationToken Create(
        Guid id,
        Guid tenantId,
        Guid accountId,
        string purpose,
        string tokenHash,
        DateTimeOffset expiresOn)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || accountId == Guid.Empty
            || string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new EntityValidationException("Verification token data is incomplete.");
        }

        return new VerificationToken
        {
            Id = id,
            TenantId = tenantId,
            AccountId = accountId,
            Purpose = purpose,
            TokenHash = tokenHash,
            ExpiresOn = expiresOn,
        };
    }

    public void Consume(DateTimeOffset consumedOn)
    {
        if (ConsumedOn is not null || consumedOn >= ExpiresOn)
        {
            throw new EntityValidationException("Verification token is no longer valid.");
        }

        ConsumedOn = consumedOn;
    }
}
