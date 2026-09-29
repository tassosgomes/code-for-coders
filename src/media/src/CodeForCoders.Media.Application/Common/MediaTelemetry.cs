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
    public static readonly Counter<long> UploadsCreated = Meter.CreateCounter<long>(
        "media.upload.created",
        unit: "{upload}");
    public static readonly Counter<long> UploadsCompleted = Meter.CreateCounter<long>(
        "media.upload.completed",
        unit: "{upload}");
    public static readonly Histogram<long> UploadSize = Meter.CreateHistogram<long>(
        "media.upload.size",
        unit: "By",
        advice: new InstrumentAdvice<long>
        {
            HistogramBucketBoundaries =
            [
                1L * 1024 * 1024,
                5L * 1024 * 1024,
                10L * 1024 * 1024,
                25L * 1024 * 1024,
                50L * 1024 * 1024,
                64L * 1024 * 1024,
                100L * 1024 * 1024,
                250L * 1024 * 1024,
                500L * 1024 * 1024,
                1L * 1024 * 1024 * 1024,
                2L * 1024 * 1024 * 1024,
                3L * 1024 * 1024 * 1024,
                4L * 1024 * 1024 * 1024,
                5L * 1024 * 1024 * 1024,
            ],
        });
    public static readonly Counter<long> UploadsExpired = Meter.CreateCounter<long>(
        "media.upload.expired",
        unit: "{upload}");
}
