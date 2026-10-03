using System.Diagnostics.Metrics;
using System.Net;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class PlaybackSignalsTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(OpenPlaybackSession_EmitsPlaybackOpenedAndDecisionDurationSignals_WithoutPersonalData))]
    public async Task OpenPlaybackSession_EmitsPlaybackOpenedAndDecisionDurationSignals_WithoutPersonalData()
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, ct);

        var openedRecords = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
        var durationRecords = new List<(double Value, KeyValuePair<string, object?>[] Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, current) =>
        {
            if (inst.Meter.Name == MediaTelemetry.MeterName)
            {
                if (inst.Name == "media.playback.opened") current.EnableMeasurementEvents(inst);
                if (inst.Name == "media.decision.duration") current.EnableMeasurementEvents(inst);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, _) =>
        {
            if (inst.Name == "media.playback.opened")
                openedRecords.Add((val, tags.ToArray()));
        });
        listener.SetMeasurementEventCallback<double>((inst, val, tags, _) =>
        {
            if (inst.Name == "media.decision.duration")
                durationRecords.Add((val, tags.ToArray()));
        });
        listener.Start();

        using var client = test.Client();
        using var response = await client.PostAsync($"/internal/v1/lessons/{test.Lesson:D}/playback-sessions", null, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.NotEmpty(openedRecords);
        var opened = openedRecords.Last();
        Assert.Equal(1L, opened.Value);
        Assert.Empty(opened.Tags);

        Assert.NotEmpty(durationRecords);
        var duration = durationRecords.Last();
        Assert.True(duration.Value > 0.0);
        Assert.Empty(duration.Tags);
    }

    [Theory(DisplayName = nameof(OpenPlaybackSession_WhenRejected_EmitsPlaybackRejectedWithNormalizedReason_AndZeroPersonalData))]
    [InlineData("denied", "negada", 403)]
    [InlineData("unavailable", "indisponivel", 503)]
    [InlineData("missing", "referencia_ausente", 404)]
    [InlineData("not_ready", "video_nao_pronto", 409)]
    [InlineData("no_email", "sem_email", 422)]
    public async Task OpenPlaybackSession_WhenRejected_EmitsPlaybackRejectedWithNormalizedReason_AndZeroPersonalData(
        string scenario,
        string expectedReason,
        int expectedStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(scenario != "missing" && scenario != "not_ready", ct);
        factory.PlaybackDecisions[test.Tenant] = scenario == "denied" ? 403 : scenario == "unavailable" ? 503 : 200;

        var rejectedRecords = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, current) =>
        {
            if (inst.Meter.Name == MediaTelemetry.MeterName && inst.Name == "media.playback.rejected")
            {
                current.EnableMeasurementEvents(inst);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, _) =>
        {
            if (inst.Name == "media.playback.rejected")
                rejectedRecords.Add((val, tags.ToArray()));
        });
        listener.Start();

        using var client = scenario == "no_email" ? test.Client(email: null) : test.Client();
        var lesson = scenario == "missing" ? Guid.CreateVersion7() : test.Lesson;
        using var response = await client.PostAsync($"/internal/v1/lessons/{lesson:D}/playback-sessions", null, ct);
        Assert.Equal(expectedStatus, (int)response.StatusCode);

        Assert.NotEmpty(rejectedRecords);
        var rejected = rejectedRecords.Last();
        Assert.Equal(1L, rejected.Value);

        var reasonTag = Assert.Single(rejected.Tags);
        Assert.Equal("reason", reasonTag.Key);
        Assert.Equal(expectedReason, reasonTag.Value);

        // Negative check: verify no personal data keys exist
        Assert.DoesNotContain(rejected.Tags, tag => tag.Key.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(rejected.Tags, tag => tag.Key.Contains("student", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = nameof(AccessDecision_WhenUnavailable_EmitsDecisionFailedCounter))]
    public async Task AccessDecision_WhenUnavailable_EmitsDecisionFailedCounter()
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, ct);
        factory.PlaybackDecisions[test.Tenant] = 503;

        var failedRecords = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, current) =>
        {
            if (inst.Meter.Name == MediaTelemetry.MeterName && inst.Name == "media.decision.failed")
            {
                current.EnableMeasurementEvents(inst);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, _) =>
        {
            if (inst.Name == "media.decision.failed")
                failedRecords.Add((val, tags.ToArray()));
        });
        listener.Start();

        using var client = test.Client();
        using var response = await client.PostAsync($"/internal/v1/lessons/{test.Lesson:D}/playback-sessions", null, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        Assert.NotEmpty(failedRecords);
        var failed = failedRecords.Last();
        Assert.Equal(1L, failed.Value);
        Assert.Empty(failed.Tags);
    }

    [Fact(DisplayName = nameof(MediaVolumeMetricsWorker_EmitsActiveStudentsGauge))]
    public async Task MediaVolumeMetricsWorker_EmitsActiveStudentsGauge()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Clean previous sessions to have a deterministic baseline
        await db.Database.ExecuteSqlRawAsync("DELETE FROM media_access.playback_sessions", ct);

        var tenantId = Guid.CreateVersion7();
        var student1 = Guid.CreateVersion7();
        var student2 = Guid.CreateVersion7();
        var student3Old = Guid.CreateVersion7();
        var lessonId = Guid.CreateVersion7();
        var courseId = Guid.CreateVersion7();
        var videoId = Guid.CreateVersion7();

        // Student 1: 2 sessions in the last 30 days
        var s1 = PlaybackSession.Create(new PlaybackSessionCreateInput(tenantId, student1, lessonId, courseId, videoId, now.AddDays(-2)));
        var s2 = PlaybackSession.Create(new PlaybackSessionCreateInput(tenantId, student1, lessonId, courseId, videoId, now.AddDays(-10)));
        // Student 2: 1 session in the last 30 days
        var s3 = PlaybackSession.Create(new PlaybackSessionCreateInput(tenantId, student2, lessonId, courseId, videoId, now.AddDays(-20)));
        // Student 3: 1 session 35 days ago (should not count)
        var s4 = PlaybackSession.Create(new PlaybackSessionCreateInput(tenantId, student3Old, lessonId, courseId, videoId, now.AddDays(-35)));

        db.PlaybackSessions.AddRange(s1, s2, s3, s4);
        await db.SaveChangesAsync(ct);

        long activeStudentsValue = -1;
        KeyValuePair<string, object?>[] capturedTags = [];

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, current) =>
        {
            if (inst.Meter.Name == MediaTelemetry.MeterName && inst.Name == "media.students.active")
            {
                current.EnableMeasurementEvents(inst);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, _) =>
        {
            if (inst.Name == "media.students.active")
            {
                activeStudentsValue = val;
                capturedTags = tags.ToArray();
            }
        });
        listener.Start();

        var worker = ActivatorUtilities.CreateInstance<MediaVolumeMetricsWorker>(factory.Services);
        await worker.RefreshAsync(ct);
        listener.RecordObservableInstruments();

        Assert.Equal(2L, activeStudentsValue);
        Assert.Empty(capturedTags);
    }

    [Fact(DisplayName = nameof(ExportedTelemetry_ContainsZeroEmailMatches))]
    public async Task ExportedTelemetry_ContainsZeroEmailMatches()
    {
        var ct = TestContext.Current.CancellationToken;
        var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, ct);

        const string personalEmail = "aluno.inspecao.sinais@exemplo.com";
        var captured = new List<(string Instrument, string Data)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, current) =>
        {
            if (inst.Meter.Name == MediaTelemetry.MeterName)
            {
                current.EnableMeasurementEvents(inst);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, _) =>
        {
            var tagList = tags.ToArray();
            var tagStr = string.Join(";", tagList.Select(t => $"{t.Key}={t.Value}"));
            captured.Add((inst.Name, $"{val} {tagStr}"));
        });
        listener.SetMeasurementEventCallback<double>((inst, val, tags, _) =>
        {
            var tagList = tags.ToArray();
            var tagStr = string.Join(";", tagList.Select(t => $"{t.Key}={t.Value}"));
            captured.Add((inst.Name, $"{val} {tagStr}"));
        });
        listener.Start();

        using var client = test.Client(email: personalEmail);
        using var response = await client.PostAsync($"/internal/v1/lessons/{test.Lesson:D}/playback-sessions", null, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.NotEmpty(captured);
        foreach (var (_, data) in captured)
        {
            Assert.DoesNotContain(personalEmail, data, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("email", data, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("student", data, StringComparison.OrdinalIgnoreCase);
        }
    }
}
