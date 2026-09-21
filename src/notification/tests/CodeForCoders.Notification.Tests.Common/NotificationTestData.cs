using CodeForCoders.Notification.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.Notification.Tests.Common;

public static class NotificationTestData
{
    public static Guid NewTenantId() => Guid.CreateVersion7();

    public static RecordPlatformHeartbeatInput NewHeartbeatInput(Guid? tenantId = null) =>
        new(tenantId ?? NewTenantId());
}
