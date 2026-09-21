namespace CodeForCoders.Commerce.Application.Common;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void Set(Guid tenantId)
    {
        TenantId = tenantId;
    }
}
