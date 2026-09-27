namespace CodeForCoders.Media.Application.Common;

public interface ITenantContext
{
    Guid? TenantId { get; }

    Guid? ActorAccountId { get; }

    void Set(Guid tenantId, Guid? actorAccountId = null);
}
