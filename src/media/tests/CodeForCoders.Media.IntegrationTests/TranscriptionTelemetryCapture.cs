using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CodeForCoders.Media.Application.Common;

namespace CodeForCoders.Media.IntegrationTests;

internal sealed class TranscriptionTelemetryCapture : IDisposable
{
    public sealed record MetricValue(string Name, double Value, KeyValuePair<string, object?>[] Tags);

    private readonly MeterListener meter = new();
    private readonly ActivityListener activities;
    public ConcurrentQueue<MetricValue> Measurements { get; } = new();
    public ConcurrentQueue<Activity> Activities { get; } = new();

    public TranscriptionTelemetryCapture()
    {
        var captureObservable = false;
        meter.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter == MediaTelemetry.Meter && instrument.Name.StartsWith("media.transcription.", StringComparison.Ordinal)
                && (!instrument.IsObservable || captureObservable)) listener.EnableMeasurementEvents(instrument);
        };
        meter.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Measurements.Enqueue(new(instrument.Name, value, tags.ToArray())));
        meter.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Measurements.Enqueue(new(instrument.Name, value, tags.ToArray())));
        meter.Start();
        captureObservable = true;
        activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MediaTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName.StartsWith("media.transcription.", StringComparison.Ordinal)) Activities.Enqueue(activity);
            },
        };
        ActivitySource.AddActivityListener(activities);
    }

    public MetricValue[] Values(string name) => Measurements.Where(value => value.Name == name).ToArray();
    public void Observe() => meter.RecordObservableInstruments();
    public void Dispose() { meter.Dispose(); activities.Dispose(); }
}
