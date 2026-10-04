using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeForCoders.Learning.Application.Common;

public static class LearningTelemetry
{
    public const string ServiceName = "CodeForCoders.Learning";
    public const string ActivitySourceName = ServiceName;
    public const string MeterName = ServiceName;

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> PlaybackProgressConsumed = Meter.CreateCounter<long>("learning.playback_progress.consumed", "{fact}");
    public static readonly Counter<long> PlaybackProgressDeadLettered = Meter.CreateCounter<long>("learning.playback_progress.dead_lettered", "{fact}");
    public static readonly Histogram<double> PlaybackProgressLag = Meter.CreateHistogram<double>("learning.playback_progress.lag", "s");
    public static readonly Counter<long> HeartbeatsRecorded = Meter.CreateCounter<long>(
        "learning.platform.heartbeat.recorded",
        unit: "{heartbeat}");
    public static readonly Counter<long> HeartbeatsConsumed = Meter.CreateCounter<long>(
        "learning.platform.heartbeat.consumed",
        unit: "{heartbeat}");
}
