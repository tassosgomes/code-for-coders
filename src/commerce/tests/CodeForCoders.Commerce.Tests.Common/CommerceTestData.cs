using CodeForCoders.Commerce.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.Commerce.Tests.Common;

public static class CommerceTestData
{
    public static Guid NewTenantId() => Guid.CreateVersion7();

    public static RecordPlatformHeartbeatInput NewHeartbeatInput(Guid? tenantId = null) =>
        new(tenantId ?? NewTenantId());
}
