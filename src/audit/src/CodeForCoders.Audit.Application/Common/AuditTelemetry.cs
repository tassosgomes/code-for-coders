using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.Audit.Application.Common;

public static class AuditTelemetry
{
    public const string ServiceName = "CodeForCoders.Audit";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> ActsRecorded = Meter.CreateCounter<long>(
        "audit.acts.recorded",
        unit: "{act}");
    public static readonly Counter<long> ActsNonconforming = Meter.CreateCounter<long>(
        "audit.acts.nonconforming",
        unit: "{act}");
    public static readonly Counter<long> ActsRedelivered = Meter.CreateCounter<long>(
        "audit.acts.redelivered",
        unit: "{act}");
    public static readonly Counter<long> MessagesIllegible = Meter.CreateCounter<long>(
        "audit.messages.illegible",
        unit: "{message}");
    public static readonly Counter<long> ComplementsRecorded = Meter.CreateCounter<long>(
        "audit.complements.recorded",
        unit: "{complement}");
    public static readonly Counter<long> ComplementsRedelivered = Meter.CreateCounter<long>(
        "audit.complements.redelivered",
        unit: "{complement}");
    public static readonly Counter<long> ComplementsRejected = Meter.CreateCounter<long>(
        "audit.complements.rejected",
        unit: "{message}");
}
