namespace CodeForCoders.BffStudent.Application.Common;

public interface ITenantContext
{
    Guid? TenantId { get; }

    void Set(Guid tenantId);
}
