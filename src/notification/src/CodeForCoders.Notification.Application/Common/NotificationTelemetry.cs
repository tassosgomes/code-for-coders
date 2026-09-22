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
    public static readonly Counter<long> NotificationsAccepted = Meter.CreateCounter<long>(
        "notification.send_request.accepted",
        unit: "{request}");
    public static readonly Counter<long> NotificationsRefused = Meter.CreateCounter<long>(
        "notification.send_request.refused",
        unit: "{request}");
    public static readonly Counter<long> NotificationsDelivered = Meter.CreateCounter<long>(
        "notification.message.delivered",
        unit: "{message}");
    public static readonly Counter<long> NotificationsManualTreatmentRequired = Meter.CreateCounter<long>(
        "notification.delivery.manual_treatment_required",
        unit: "{request}");
}
