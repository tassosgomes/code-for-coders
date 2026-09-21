namespace CodeForCoders.Learning.Domain.SeedWork;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new EntityValidationException("Tenant id must not be empty.");
        }

        return new TenantId(value);
    }
}
