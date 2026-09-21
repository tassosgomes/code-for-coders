using CodeForCoders.Media.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.Media.Tests.Common;

public static class MediaTestData
{
    public static Guid NewTenantId() => Guid.CreateVersion7();

    public static RecordPlatformHeartbeatInput NewHeartbeatInput(Guid? tenantId = null) =>
        new(tenantId ?? NewTenantId());
}
