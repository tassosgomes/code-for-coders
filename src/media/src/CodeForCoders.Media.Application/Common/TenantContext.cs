namespace CodeForCoders.Media.Application.Common;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public Guid? ActorAccountId { get; private set; }

    public void Set(Guid tenantId, Guid? actorAccountId = null)
    {
        TenantId = tenantId;
        ActorAccountId = actorAccountId;
    }
}
