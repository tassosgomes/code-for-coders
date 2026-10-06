using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.Billing.Application.Common;

public static class BillingTelemetry
{
    public const string ServiceName = "CodeForCoders.Billing";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "billing.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "billing.platform.heartbeat.consumed",
        unit: "{heartbeat}");
    public static readonly Counter<long> BillingsAccepted = Meter.CreateCounter<long>(
        "billing.send_request.accepted",
        unit: "{request}");
    public static readonly Counter<long> BillingsRefused = Meter.CreateCounter<long>(
        "billing.send_request.refused",
        unit: "{request}");
    public static readonly Counter<long> BillingsDelivered = Meter.CreateCounter<long>(
        "billing.message.delivered",
        unit: "{message}");
    public static readonly Counter<long> BillingsManualTreatmentRequired = Meter.CreateCounter<long>(
        "billing.delivery.manual_treatment_required",
        unit: "{request}");
    public static readonly Counter<long> GatewayEventsReceived = Meter.CreateCounter<long>(
        "billing.gateway.events.received",
        unit: "{event}");
    public static readonly Counter<long> GatewaySignaturesRefused = Meter.CreateCounter<long>(
        "billing.gateway.signatures.refused",
        unit: "{request}");
}
