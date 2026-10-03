using System.Diagnostics;
using System.Diagnostics.Metrics;
using CodeForCoders.Media.Application.Common;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class PlaybackTelemetryTests
{
    [Fact]
    public void RecordPlaybackOpened_EmitsOpenedCounterWithoutPersonalData()
    {
        var measurements = CaptureMeasurements(
            () => MediaTelemetry.RecordPlaybackOpened(),
            "media.playback.opened");

        Assert.NotEmpty(measurements);
        var (instrument, value, tags) = measurements.Last();
        Assert.Equal("media.playback.opened", instrument.Name);
        Assert.Equal("{session}", instrument.Unit);
        Assert.Equal(1L, value);
        Assert.Empty(tags);
    }

    [Theory]
    [InlineData("referencia_ausente", "referencia_ausente")]
    [InlineData("LESSON_NOT_AVAILABLE", "referencia_ausente")]
    [InlineData("video_nao_pronto", "video_nao_pronto")]
    [InlineData("MEDIA_NOT_READY", "video_nao_pronto")]
    [InlineData("indisponivel", "indisponivel")]
    [InlineData("ACCESS_DECISION_UNAVAILABLE", "indisponivel")]
    [InlineData("negada", "negada")]
    [InlineData("ACCESS_DENIED", "negada")]
    [InlineData("sem_email", "sem_email")]
    [InlineData("WATERMARK_UNAVAILABLE", "sem_email")]
    public void RecordPlaybackRejected_EmitsNormalizedReasons_AndStrictlyNoPersonalData(string inputReason, string expectedReason)
    {
        var measurements = CaptureMeasurements(
            () => MediaTelemetry.RecordPlaybackRejected(inputReason),
            "media.playback.rejected");

        Assert.NotEmpty(measurements);
        var (instrument, value, tags) = measurements.Last();
        Assert.Equal("media.playback.rejected", instrument.Name);
        Assert.Equal("{session}", instrument.Unit);
        Assert.Equal(1L, value);

        var reasonTag = Assert.Single(tags);
        Assert.Equal("reason", reasonTag.Key);
        Assert.Equal(expectedReason, reasonTag.Value);

        // Negative check: verify no personal data keys exist
        Assert.DoesNotContain(tags, tag => tag.Key.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tags, tag => tag.Key.Contains("student", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tags, tag => tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RecordDecisionDuration_RecordsDurationInSecondsWithoutPersonalData()
    {
        var startedAt = Stopwatch.GetTimestamp();
        Thread.Sleep(10);

        var measurements = CaptureMeasurements(
            () => MediaTelemetry.RecordDecisionDuration(startedAt),
            "media.decision.duration");

        Assert.NotEmpty(measurements);
        var (instrument, value, tags) = measurements.Last();
        Assert.Equal("media.decision.duration", instrument.Name);
        Assert.Equal("s", instrument.Unit);
        var seconds = Assert.IsType<double>(value);
        Assert.True(seconds > 0.0);
        Assert.Empty(tags);
    }

    [Fact]
    public void RecordDecisionFailed_EmitsFailedCounterWithoutPersonalData()
    {
        var measurements = CaptureMeasurements(
            () => MediaTelemetry.RecordDecisionFailed(),
            "media.decision.failed");

        Assert.NotEmpty(measurements);
        var (instrument, value, tags) = measurements.Last();
        Assert.Equal("media.decision.failed", instrument.Name);
        Assert.Equal("{call}", instrument.Unit);
        Assert.Equal(1L, value);
        Assert.Empty(tags);
    }

    [Fact]
    public void ObservabilidadeScript_RejectsManifestInstrumentWithoutProducer()
    {
        var pythonScript = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "kibana", "import_observabilidade.py");
        if (!File.Exists(pythonScript))
        {
            pythonScript = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../../scripts/kibana/import_observabilidade.py"));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = $"-c \"import sys; sys.path.insert(0, '{Path.GetDirectoryName(pythonScript)}'); import import_observabilidade; import_observabilidade.INSTRUMENT_FIELDS['media.ghost.instrument'] = 'metrics.media.ghost.instrument'; old = import_observabilidade.nested_strings; import_observabilidade.nested_strings = lambda p: old(p) + ' metrics.media.ghost.instrument'; import_observabilidade.verify_saved_objects()\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        process.WaitForExit(10000);
        Assert.NotEqual(0, process.ExitCode);
        var err = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
        Assert.Contains("métrica do manifesto sem produtor no código de produção", err);
    }

    [Fact]
    public void ObservabilidadeScript_RejectsNdjsonWithMissingInstrument()
    {
        // Negative test from spec: verify that missing manifest instrument in NDJSON is rejected
        var pythonScript = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "kibana", "import_observabilidade.py");
        if (!File.Exists(pythonScript))
        {
            pythonScript = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../../scripts/kibana/import_observabilidade.py"));
        }

        var tempNdjson = Path.GetTempFileName();
        try
        {
            var originalNdjson = Path.Combine(Path.GetDirectoryName(pythonScript)!, "observabilidade-midia.ndjson");
            var lines = File.ReadAllLines(originalNdjson).ToList();

            // Corrupt the ndjson by replacing "media.playback.opened" with a dummy field name
            var corrupted = lines.Select(l => l.Replace("metrics.media.playback.opened", "metrics.media.playback.corrupted")).ToArray();
            File.WriteAllLines(tempNdjson, corrupted);

            var startInfo = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"-c \"import sys; sys.path.insert(0, '{Path.GetDirectoryName(pythonScript)}'); import import_observabilidade; import pathlib; import_observabilidade.verify_saved_objects(pathlib.Path('{tempNdjson}'))\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            process.WaitForExit(10000);
            Assert.NotEqual(0, process.ExitCode);
            var err = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            Assert.Contains("métrica do manifesto não referenciada", err);
        }
        finally
        {
            File.Delete(tempNdjson);
        }
    }

    private static List<(Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)> CaptureMeasurements(
        Action action,
        params string[] instrumentNames)
    {
        var captured = new List<(Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, current) =>
        {
            if (inst.Meter.Name == MediaTelemetry.MeterName && instrumentNames.Contains(inst.Name))
            {
                current.EnableMeasurementEvents(inst);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, _) =>
        {
            captured.Add((inst, val, tags.ToArray()));
        });
        listener.SetMeasurementEventCallback<double>((inst, val, tags, _) =>
        {
            captured.Add((inst, val, tags.ToArray()));
        });
        listener.Start();
        action();
        return captured;
    }
}
