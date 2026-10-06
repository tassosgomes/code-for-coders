using CodeForCoders.Billing.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.Billing.Tests.Common;

public static class BillingTestData
{
    public static Guid NewTenantId() => Guid.CreateVersion7();

    public static RecordPlatformHeartbeatInput NewHeartbeatInput(Guid? tenantId = null) =>
        new(tenantId ?? NewTenantId());
}
