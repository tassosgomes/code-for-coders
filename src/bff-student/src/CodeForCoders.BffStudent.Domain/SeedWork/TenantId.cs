namespace CodeForCoders.BffStudent.Domain.SeedWork;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new EntityValidationException("Tenant id cannot be empty.");
        }

        return new TenantId(value);
    }
}
