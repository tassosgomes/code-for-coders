namespace CodeForCoders.Billing.Application.Common;

public interface ITenantContext
{
    string Namespace { get; }

    Guid? TenantId { get; }

    void SetNamespace(string processingNamespace);

    void Set(Guid tenantId);
}
