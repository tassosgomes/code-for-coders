using CodeForCoders.Learning.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.Learning.Tests.Common;

public static class LearningTestData
{
    public static Guid NewTenantId() => Guid.CreateVersion7();

    public static RecordPlatformHeartbeatInput NewHeartbeatInput(Guid? tenantId = null) =>
        new(tenantId ?? NewTenantId());
}
