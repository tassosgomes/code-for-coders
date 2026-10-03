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
    public static readonly Counter<long> VideosClaimed = Meter.CreateCounter<long>(
        "media.videos.claimed",
        unit: "{video}");
    public static readonly Counter<long> VideosRetried = Meter.CreateCounter<long>(
        "media.videos.retried",
        unit: "{video}");
    public static readonly Histogram<double> VideoWait = Meter.CreateHistogram<double>(
        "media.videos.wait",
        unit: "s",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [1, 5, 10, 30, 60, 300, 600, 1800, 3600],
        });
    public static readonly Counter<long> VideosCompleted = Meter.CreateCounter<long>(
        "media.videos.completed",
        unit: "{video}");
    public static readonly Counter<long> VideosFailed = Meter.CreateCounter<long>(
        "media.videos.failed",
        unit: "{video}");
    public static readonly Histogram<double> VideoPrepareDuration = Meter.CreateHistogram<double>(
        "media.videos.prepare_duration",
        unit: "s",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.1, 0.5, 1, 5, 10, 30, 60, 300, 600, 1800, 3600, 10800, 86400],
        });
    public static readonly Histogram<double> VideoTimeToReady = Meter.CreateHistogram<double>(
        "media.videos.time_to_ready",
        unit: "s",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [10, 30, 60, 300, 600, 1800, 3600, 10800, 21600, 43200, 86400],
        });
    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>(
        "media.outbox.published",
        unit: "{message}");
    public static readonly Counter<long> OutboxPublishFailed = Meter.CreateCounter<long>(
        "media.outbox.publish_failed",
        unit: "{message}");
    public static readonly Counter<long> PlaybackOpened = Meter.CreateCounter<long>(
        "media.playback.opened",
        unit: "{session}");
    public static readonly Counter<long> PlaybackRejected = Meter.CreateCounter<long>(
        "media.playback.rejected",
        unit: "{session}");
    public static readonly Histogram<double> DecisionDuration = Meter.CreateHistogram<double>(
        "media.decision.duration",
        unit: "s",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2, 5],
        });
    public static readonly Counter<long> DecisionFailed = Meter.CreateCounter<long>(
        "media.decision.failed",
        unit: "{call}");

    public static void RecordOutboxPublished(string routingKey)
        => RecordOutboxEvent(OutboxPublished, routingKey);

    public static void RecordOutboxPublishFailed(string routingKey)
        => RecordOutboxEvent(OutboxPublishFailed, routingKey);

    public static void RecordVideoPrepareDuration(string stage, long startedAt)
        => VideoPrepareDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
            new KeyValuePair<string, object?>("stage", stage));

    public static void RecordPlaybackOpened()
        => PlaybackOpened.Add(1);

    public static void RecordPlaybackRejected(string reason)
        => PlaybackRejected.Add(1, new KeyValuePair<string, object?>("reason", NormalizePlaybackReason(reason)));

    public static void RecordDecisionDuration(long startedAt)
        => DecisionDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds);

    public static void RecordDecisionFailed()
        => DecisionFailed.Add(1);

    public static string NormalizePlaybackReason(string reason)
        => reason switch
        {
            "LESSON_NOT_AVAILABLE" or "referencia_ausente" or "referencia-ausente" or "referência ausente" => "referencia_ausente",
            "MEDIA_NOT_READY" or "video_nao_pronto" or "video-nao-pronto" or "vídeo não pronto" => "video_nao_pronto",
            "ACCESS_DECISION_UNAVAILABLE" or "indisponivel" or "indisponível" => "indisponivel",
            "ACCESS_DENIED" or "negada" => "negada",
            "WATERMARK_UNAVAILABLE" or "sem_email" or "sem-email" or "sem e-mail" => "sem_email",
            _ => reason,
        };

    private static void RecordOutboxEvent(Counter<long> instrument, string routingKey)
    {
        var eventName = routingKey switch
        {
            "midia.ativo-pronto.v1" => "ativo-pronto",
            "midia.preparacao-falhou.v1" => "preparacao-falhou",
            _ => null,
        };

        if (eventName is not null)
        {
            instrument.Add(1, new KeyValuePair<string, object?>("event", eventName));
        }
    }
}
