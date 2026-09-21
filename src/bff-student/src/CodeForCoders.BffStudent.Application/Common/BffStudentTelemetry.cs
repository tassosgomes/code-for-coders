using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.BffStudent.Application.Common;

public static class BffStudentTelemetry
{
    public const string ServiceName = "CodeForCoders.BffStudent";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "bff_student.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "bff_student.platform.heartbeat.consumed",
        unit: "{heartbeat}");
}
