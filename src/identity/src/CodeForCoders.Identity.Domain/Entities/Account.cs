using CodeForCoders.Identity.Domain.SeedWork;

namespace CodeForCoders.Identity.Domain.Entities;

public sealed class Account
{
    private Account()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public AccountType Type { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public bool IsConfirmed { get; private set; }

    public DateTimeOffset? DeactivatedOn { get; private set; }

    public static Account CreateStudent(
        Guid id,
        Guid tenantId,
        string name,
        string email,
        string normalizedEmail)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new EntityValidationException("Account identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new EntityValidationException("Account name and email are required.");
        }

        return new Account
        {
            Id = id,
            TenantId = tenantId,
            Type = AccountType.Student,
            Name = name.Trim(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
        };
    }

    public static Account CreateInternal(
        Guid id,
        Guid tenantId,
        string name,
        string email,
        string normalizedEmail)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new EntityValidationException("Account identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new EntityValidationException("Account name and email are required.");
        }

        return new Account
        {
            Id = id,
            TenantId = tenantId,
            Type = AccountType.InternalActor,
            Name = name.Trim(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            IsConfirmed = true,
        };
    }

    public void Confirm()
    {
        if (Type != AccountType.Student)
        {
            throw new EntityValidationException("Only student accounts can be confirmed through email.");
        }

        IsConfirmed = true;
    }
}
