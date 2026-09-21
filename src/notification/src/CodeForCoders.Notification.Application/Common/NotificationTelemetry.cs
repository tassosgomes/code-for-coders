using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.Notification.Application.Common;

public static class NotificationTelemetry
{
    public const string ServiceName = "CodeForCoders.Notification";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "notification.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "notification.platform.heartbeat.consumed",
        unit: "{heartbeat}");
}
