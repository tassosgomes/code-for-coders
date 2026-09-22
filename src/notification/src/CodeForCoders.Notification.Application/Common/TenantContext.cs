using CodeForCoders.Notification.Domain.SeedWork;

namespace CodeForCoders.Notification.Application.Common;

public sealed class TenantContext : ITenantContext
{
    private string processingNamespace;

    public TenantContext(string processingNamespace = "default")
    {
        this.processingNamespace = NotificationNamespace.Validate(processingNamespace);
    }

    public string Namespace => processingNamespace;

    public Guid? TenantId { get; private set; }

    public void SetNamespace(string processingNamespace)
    {
        this.processingNamespace = NotificationNamespace.Validate(processingNamespace);
    }

    public void Set(Guid tenantId)
    {
        TenantId = tenantId;
    }
}
