using CodeForCoders.Identity.Domain.SeedWork;

namespace CodeForCoders.Identity.Domain.Entities;

public sealed class StaffRoleAssignment
{
    private StaffRoleAssignment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public string Role { get; private set; } = string.Empty;

    public DateTimeOffset AssignedOn { get; private set; }

    public static StaffRoleAssignment Create(
        Guid id,
        Guid tenantId,
        Guid accountId,
        string role,
        DateTimeOffset assignedOn)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new EntityValidationException("Staff role assignment identifiers are required.");
        }

        if (!StaffRoleCatalog.Contains(role))
        {
            throw new EntityValidationException("The staff role is not in the catalog.");
        }

        return new StaffRoleAssignment
        {
            Id = id,
            TenantId = tenantId,
            AccountId = accountId,
            Role = role,
            AssignedOn = assignedOn,
        };
    }
}
