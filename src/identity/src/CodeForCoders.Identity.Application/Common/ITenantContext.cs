namespace CodeForCoders.Identity.Application.Common;

public interface ITenantContext
{
    Guid? TenantId { get; }

    void Set(Guid tenantId);
}
