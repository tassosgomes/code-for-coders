using CodeForCoders.BffStudent.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.BffStudent.Tests.Common;

public static class BffStudentTestData
{
    public static Guid NewTenantId() => Guid.CreateVersion7();

    public static RecordPlatformHeartbeatInput NewHeartbeatInput(Guid? tenantId = null) =>
        new(tenantId ?? NewTenantId());
}
