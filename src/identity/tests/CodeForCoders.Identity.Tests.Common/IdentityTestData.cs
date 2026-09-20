using CodeForCoders.Identity.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.Identity.Tests.Common;

public static class IdentityTestData
{
    public static Guid NewTenantId() => Guid.CreateVersion7();

    public static RecordPlatformHeartbeatInput NewHeartbeatInput(Guid? tenantId = null) =>
        new(tenantId ?? NewTenantId());
}
