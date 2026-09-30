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
    public static readonly Counter<long> CourseFactsApplied = Meter.CreateCounter<long>("commerce.catalog.course.applied", "{fact}");
    public static readonly Counter<long> CourseFactsIgnored = Meter.CreateCounter<long>("commerce.catalog.course.ignored", "{fact}");
    public static readonly Counter<long> CourseFactsDeadLettered = Meter.CreateCounter<long>("commerce.catalog.course.dead_lettered", "{fact}");
    public static readonly Histogram<double> CourseFactLag = Meter.CreateHistogram<double>("commerce.catalog.course.lag", "s");
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "commerce.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "commerce.platform.heartbeat.consumed",
        unit: "{heartbeat}");
}
