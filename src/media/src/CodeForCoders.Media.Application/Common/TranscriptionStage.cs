using System.Diagnostics;

namespace CodeForCoders.Media.Application.Common;

public sealed class TranscriptionStage : IDisposable
{
    private readonly string stage;
    private readonly long startedAt = Stopwatch.GetTimestamp();
    private bool disposed;

    internal TranscriptionStage(string stage, Guid? videoId, Guid? transcriptionId)
    {
        this.stage = stage;
        Activity = MediaTelemetry.ActivitySource.StartActivity("media.transcription." + stage);
        if (videoId is { } video) Activity?.SetTag("video.id", video.ToString("D"));
        if (transcriptionId is { } attempt) Activity?.SetTag("transcription.id", attempt.ToString("D"));
    }

    public Activity? Activity { get; }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        MediaTelemetry.TranscriptionStageDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
            new KeyValuePair<string, object?>("stage", stage));
        Activity?.Dispose();
    }
}
