using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.BffAdmin.Application.Common;

public static class BffAdminTelemetry
{
    public const string ServiceName = "CodeForCoders.BffAdmin";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "bff_admin.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "bff_admin.platform.heartbeat.consumed",
        unit: "{heartbeat}");
}
