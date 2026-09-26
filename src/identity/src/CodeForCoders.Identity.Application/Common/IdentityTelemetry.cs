using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.Identity.Application.Common;

public static class IdentityTelemetry
{
    public const string ServiceName = "CodeForCoders.Identity";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "identity.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "identity.platform.heartbeat.consumed",
        unit: "{heartbeat}");
    public static readonly Counter<long> AuditActionsPublished = Meter.CreateCounter<long>(
        "identity.audit.action.published",
        unit: "{action}");
}
