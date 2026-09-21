using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.Media.Application.Common;

public static class MediaTelemetry
{
    public const string ServiceName = "CodeForCoders.Media";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "media.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "media.platform.heartbeat.consumed",
        unit: "{heartbeat}");
}
