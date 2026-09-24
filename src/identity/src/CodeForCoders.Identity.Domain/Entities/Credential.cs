using CodeForCoders.Identity.Domain.SeedWork;

namespace CodeForCoders.Identity.Domain.Entities;

public sealed class Credential
{
    private Credential()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedOn { get; private set; }

    public static Credential Create(Guid id, Guid tenantId, Guid accountId, string passwordHash, DateTimeOffset createdOn)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new EntityValidationException("Credential identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new EntityValidationException("A derived password is required.");
        }

        return new Credential
        {
            Id = id,
            TenantId = tenantId,
            AccountId = accountId,
            PasswordHash = passwordHash,
            CreatedOn = createdOn,
        };
    }

    public void ReplacePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new EntityValidationException("A derived password is required.");
        }

        PasswordHash = passwordHash;
    }
}
