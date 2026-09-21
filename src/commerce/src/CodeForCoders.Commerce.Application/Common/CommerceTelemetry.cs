using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.Commerce.Application.Common;

public static class CommerceTelemetry
{
    public const string ServiceName = "CodeForCoders.Commerce";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "commerce.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "commerce.platform.heartbeat.consumed",
        unit: "{heartbeat}");
}
