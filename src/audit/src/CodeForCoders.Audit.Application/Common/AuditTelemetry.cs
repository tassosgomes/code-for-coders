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
}
