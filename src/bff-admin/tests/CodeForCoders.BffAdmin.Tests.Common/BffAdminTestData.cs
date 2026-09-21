using CodeForCoders.BffAdmin.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.BffAdmin.Tests.Common;

public static class BffAdminTestData
{
    public static Guid NewTenantId() => Guid.CreateVersion7();

    public static RecordPlatformHeartbeatInput NewHeartbeatInput(Guid? tenantId = null) =>
        new(tenantId ?? NewTenantId());
}
