using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class TranscriptionPipelineTests(VideoLibraryApiFactory factory)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EncryptedHlsBecomesMono16KhzAudioAndReadyReadableTextWithNoArtifactsOrSecrets()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.FetchAudio = true;
        await using var context = await CreateContextAsync(server);
        await UseConfiguredMediaImageAsync(context);
        var (video, id) = await EnqueuePreparedAsync(context);
        var telemetry = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MediaTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => telemetry.Enqueue(activity.DisplayName + " " + string.Join(" ", activity.TagObjects)),
        };
        ActivitySource.AddActivityListener(listener);
        Assert.True(await ExecuteAsync(context));
        var submitted = await AttemptAsync(context, id);
        Assert.Equal("waiting", submitted.Status); Assert.Equal("trn_test", submitted.WhisperJobId);
        Assert.Equal(1, submitted.Submissions); Assert.Equal(1, submitted.ExtractionAttempts);
        Assert.Equal(TimeSpan.FromHours(30), submitted.DeadlineAt - submitted.SubmittedAt);
        Assert.NotNull(server.Audio);
        var audioPath = Path.Combine(context.RootDirectory, "downloaded.m4a");
        await File.WriteAllBytesAsync(audioPath, server.Audio!, Token);
        var probe = await context.RunFfmpegAsync(["-i", audioPath, "-f", "null", "-"], Token);
        Assert.Equal(0, probe.ExitCode);
        Assert.Contains("16000 Hz", probe.StandardError); Assert.Contains("mono", probe.StandardError);
        server.Status = "completed"; await MakeEligibleAsync(context, id); Assert.True(await ExecuteAsync(context));
        var ready = await AttemptAsync(context, id);
        Assert.Equal("ready", ready.Status); Assert.True(ready.IsCurrent); Assert.NotNull(ready.ReadyAt); Assert.NotNull(ready.AudioDeletedAt);
        var state = TranscriptionState.Derive("ready", [ready], true);
        Assert.Equal("ready", state.Status); Assert.True(state.Readable); Assert.Equal(ready.ReadyAt, state.ReadyAt);
        var content = await ContentAsync(context, id); using var json = JsonDocument.Parse(content!.SegmentsJson);
        Assert.Equal("first private lesson", json.RootElement[0].GetProperty("text").GetString());
        Assert.Equal("second private lesson", json.RootElement[1].GetProperty("text").GetString());
        Assert.Equal(2, ready.SegmentCount);
        await AssertCleanAsync(context, video);
        var logs = string.Join(" ", context.Logs.Entries.Concat(telemetry));
        var submission = Assert.Single(server.Submissions);
        Assert.DoesNotContain(submission.SourceUrl, logs); Assert.DoesNotContain("private lesson", logs);
        Assert.DoesNotContain(TranscriptionHttpSimulator.ApiKey, logs);
        await using var scope = context.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var persistedVideo = await db.Videos.IgnoreQueryFilters().SingleAsync(row => row.VideoId == video.VideoId, Token);
        var key = context.UnprotectKey(video.VideoId, persistedVideo.MasterKeyId!, persistedVideo.EncryptedVideoKey!);
        try { Assert.DoesNotContain(Convert.ToBase64String(key), logs); Assert.DoesNotContain(Convert.ToHexString(key), logs); }
        finally { CryptographicOperations.ZeroMemory(key); }
        Assert.Contains(telemetry, entry => entry.StartsWith("media.transcription.extract-audio", StringComparison.Ordinal));
        Assert.Contains(telemetry, entry => entry.StartsWith("media.transcription.fetch-result", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DownloadingKeepsAudioAndProcessingDeletesItBeforeCompletion()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, id) = await EnqueuePreparedAsync(context); await ExecuteAsync(context);
        await MakeEligibleAsync(context, id); await ExecuteAsync(context);
        Assert.NotEmpty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));
        server.Status = "processing"; await MakeEligibleAsync(context, id); await ExecuteAsync(context);
        Assert.Empty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));
        var attempt = await AttemptAsync(context, id); Assert.Equal("waiting", attempt.Status); Assert.NotNull(attempt.AudioDeletedAt);
        Assert.True(attempt.NextActionAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task UnknownRemoteStateRemainsWaitingAndIsRecordedWithoutPayload()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, id) = await EnqueuePreparedAsync(context); await ExecuteAsync(context);
        server.Status = "future-state"; await MakeEligibleAsync(context, id); await ExecuteAsync(context);
        Assert.Equal("waiting", (await AttemptAsync(context, id)).Status);
        Assert.NotEmpty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));
        Assert.Contains(context.Logs.Entries, entry => entry.Contains("unknown state", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("bytes")]
    [InlineData("segments")]
    public async Task InvalidResultsFailWithoutPublishingTextAndLeaveNoAudio(string invalid)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, id) = await EnqueuePreparedAsync(context); await ExecuteAsync(context);
        server.Status = "completed";
        server.Result = invalid switch
        {
            "schema" => server.Result.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal),
            "bytes" => new string('x', TranscriptionResultValidation.MaximumJsonBytes + 1),
            _ => JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                language = "pt-BR",
                durationMs = 1,
                segments = Enumerable.Range(0, 20001).Select(_ => new { startMs = 0, endMs = 1, text = "x" })
            }),
        };
        await MakeEligibleAsync(context, id); await ExecuteAsync(context);
        var attempt = await AttemptAsync(context, id); Assert.Equal("failed", attempt.Status); Assert.Equal("processing-failed", attempt.FailureReason);
        Assert.Null(await ContentAsync(context, id)); await AssertCleanAsync(context, video);
    }

    [Theory]
    [InlineData("https://attacker.test/segment.ts")]
    [InlineData("../segment_00000.ts")]
    [InlineData("480p/../../segment_00000.ts")]
    public async Task UntrustedPlaylistResourcesFailAndErasePrivateWorkDirectory(string resource)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, id) = await EnqueuePreparedAsync(context);
        var playlist = $"#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"c4c-key:{video.VideoId:D}\"\n#EXTINF:1,\n{resource}\n#EXT-X-ENDLIST\n";
        await context.UploadOriginalBytesAsync($"{video.TenantId:D}/{video.VideoId:D}/hls/480p.m3u8", Encoding.UTF8.GetBytes(playlist), Token);
        await ExecuteAsync(context); Assert.Equal("failed", (await AttemptAsync(context, id)).Status);
        Assert.Empty(server.Submissions); await AssertCleanAsync(context, video);
    }

    [Fact]
    public async Task FfmpegFailureErasesKeyAndAudioWithoutLoggingToolOutput()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server, includeAudio: false);
        var (video, id) = await EnqueuePreparedAsync(context); await ExecuteAsync(context);
        Assert.Equal("processing-failed", (await AttemptAsync(context, id)).FailureReason);
        Assert.Empty(server.Submissions); await AssertCleanAsync(context, video);
    }

    [Fact]
    public async Task DurationLimitFailsBeforeDownloadingOrExtracting()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, id) = await EnqueuePreparedAsync(context);
        await context.WithScopeAsync(async provider =>
        {
            await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.videos SET duration_seconds = 7201 WHERE video_id = {video.VideoId}", Token); return true;
        });
        var downloads = context.DownloadAttemptCount; await ExecuteAsync(context);
        Assert.Equal("duration-limit", (await AttemptAsync(context, id)).FailureReason);
        Assert.Equal(downloads, context.DownloadAttemptCount); Assert.Empty(server.Submissions); await AssertCleanAsync(context, video);
    }

    [Fact]
    public async Task DiskBudgetLeavesExtractionQueuedButDoesNotPreventPolls()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (_, id) = await EnqueuePreparedAsync(context);
        Assert.False(await ExecuteAsync(context, disk: 1)); Assert.Equal("queued", (await AttemptAsync(context, id)).Status);
        await ExecuteAsync(context); server.Status = "completed"; await MakeEligibleAsync(context, id);
        Assert.True(await ExecuteAsync(context, disk: 1)); Assert.Equal("ready", (await AttemptAsync(context, id)).Status);
    }

    [Fact]
    public async Task TwoWorkersOnlySubmitOneJobForTheSameAttempt()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        await EnqueuePreparedAsync(context);
        var results = await Task.WhenAll(ExecuteAsync(context), ExecuteAsync(context));
        Assert.Single(results, value => value); Assert.Single(server.Submissions);
    }

    [Fact]
    public async Task DeadWorkerLeaseReturnsToQueueAndNextClaimCountsAnotherExtraction()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, id) = await EnqueuePreparedAsync(context);
        await using (var scope = context.CreateScope())
        {
            var claimed = await scope.ServiceProvider.GetRequiredService<ITranscriptionWorkerRepository>().ClaimNextAsync(new(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), true), Token);
            Assert.NotNull(claimed); Directory.CreateDirectory(claimed.LocalDirectory(context.WorkDirectory));
            await File.WriteAllTextAsync(Path.Combine(claimed.LocalDirectory(context.WorkDirectory), "key.bin"), "abandoned", Token);
        }
        await ExpireAsync(context, id);
        await MaintainAsync(context); Assert.Equal("queued", (await AttemptAsync(context, id)).Status);
        await AssertCleanAsync(context, video);
        await ExecuteAsync(context); Assert.Equal(2, (await AttemptAsync(context, id)).ExtractionAttempts); Assert.Single(server.Submissions);
    }

    [Fact]
    public async Task StaleLeaseCannotPublishAfterAnotherWorkerHasClaimed()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (_, id) = await EnqueuePreparedAsync(context); TranscriptionWorkItem stale;
        await using (var scope = context.CreateScope()) stale = (await scope.ServiceProvider.GetRequiredService<ITranscriptionWorkerRepository>().ClaimNextAsync(new(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), true), Token))!;
        await ExpireAsync(context, id); await MaintainAsync(context);
        await using var newScope = context.CreateScope(); var repository = newScope.ServiceProvider.GetRequiredService<ITranscriptionWorkerRepository>();
        var current = await repository.ClaimNextAsync(new(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), true), Token);
        Assert.NotEqual(stale.Attempt.LeaseId, current!.Attempt.LeaseId);
        Assert.False(await repository.WithLeaseAsync(stale, (_, _) => throw new InvalidOperationException("Must not run"), Token));
    }

    [Fact]
    public async Task SuccessfulReplacementRetiresOldCurrentAndDeletesOldContent()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, first) = await EnqueuePreparedAsync(context); await ExecuteAsync(context); server.Status = "completed";
        await MakeEligibleAsync(context, first); await ExecuteAsync(context);
        var second = await EnqueueAsync(context, video); await ExecuteAsync(context); await MakeEligibleAsync(context, second); await ExecuteAsync(context);
        Assert.False((await AttemptAsync(context, first)).IsCurrent); Assert.Null(await ContentAsync(context, first));
        Assert.True((await AttemptAsync(context, second)).IsCurrent); Assert.NotNull(await ContentAsync(context, second));
    }

    [Fact]
    public async Task TerminalAudioLeftByDeadWorkerIsDeletedByMaintenance()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, id) = await EnqueuePreparedAsync(context); await ExecuteAsync(context);
        await context.WithScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<MediaDbContext>(); var attempt = await db.Transcriptions.IgnoreQueryFilters().SingleAsync(row => row.TranscriptionId == id, Token);
            attempt.MarkFailed("processing-failed"); await db.SaveChangesAsync(Token); return true;
        });
        Assert.NotEmpty(await context.ListObjectKeysAsync(AudioPrefix(video), Token)); await MaintainAsync(context);
        await AssertCleanAsync(context, video); Assert.NotNull((await AttemptAsync(context, id)).AudioDeletedAt);
    }

    [Fact]
    public async Task ExpiredDeadlineFailsAndErasesAudio()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(server);
        var (video, id) = await EnqueuePreparedAsync(context); await ExecuteAsync(context);
        await context.WithScopeAsync(async provider =>
        {
            await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET deadline_at = {DateTimeOffset.UtcNow.AddMinutes(-1)}, next_action_at = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE transcription_id = {id}", Token); return true;
        });
        await ExecuteAsync(context); Assert.Equal("timed-out", (await AttemptAsync(context, id)).FailureReason); await AssertCleanAsync(context, video);
    }

    private static async Task UseConfiguredMediaImageAsync(VideoPreparationTestContext context)
    {
        var image = Environment.GetEnvironmentVariable("MEDIA_TRANSCRIPTION_FFMPEG_IMAGE");
        if (string.IsNullOrEmpty(image)) return;
        Assert.Matches("^[A-Za-z0-9/_.:@-]+$", image);
        var wrapper = Path.Combine(context.RootDirectory, "image-ffmpeg.sh");
        await File.WriteAllTextAsync(wrapper,
            $"#!/bin/sh\nexec docker run --rm --network none --user \"$(id -u):$(id -g)\" -v '{context.RootDirectory}:{context.RootDirectory}' --entrypoint ffmpeg '{image}' \"$@\"\n", Token);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await context.WithScopeAsync(provider =>
        {
            provider.GetRequiredService<IOptions<VideoPreparationOptions>>().Value.FfmpegPath = wrapper;
            return Task.FromResult(true);
        });
    }

    private Task<VideoPreparationTestContext> CreateContextAsync(TranscriptionHttpSimulator server, bool includeAudio = true)
        => VideoPreparationTestContext.CreateAsync(factory, Token, new(IncludeAudio: includeAudio, TranscriptionServiceAddress: server.BaseAddress, TranscriptionEnabled: true));

    private static async Task<(VideoPreparationTestContext.TestVideo Video, Guid Id)> EnqueuePreparedAsync(VideoPreparationTestContext context)
    {
        var video = await context.CreateQueuedVideoAsync(640, 480, 1, Token);
        Assert.True(await context.ExecuteNextAsync(long.MaxValue / 4, Token));
        var id = await context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>()
            .Transcriptions.IgnoreQueryFilters().Where(row => row.VideoId == video.VideoId)
            .Select(row => row.TranscriptionId).SingleAsync(Token));
        return (video, id);
    }

    private static Task<Guid> EnqueueAsync(VideoPreparationTestContext context, VideoPreparationTestContext.TestVideo video)
        => context.WithScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<MediaDbContext>();
            var ready = await db.Videos.IgnoreQueryFilters().SingleAsync(row => row.VideoId == video.VideoId, Token); Assert.Equal("ready", ready.Status);
            var attempt = Transcription.Create(new(video.TenantId, video.VideoId, "requested", Guid.CreateVersion7(), DateTimeOffset.UtcNow));
            db.Transcriptions.Add(attempt); await db.SaveChangesAsync(Token); return attempt.TranscriptionId;
        });

    private static Task<bool> ExecuteAsync(VideoPreparationTestContext context, long disk = long.MaxValue / 4)
        => context.WithScopeAsync(provider => provider.GetRequiredService<ITranscriptionWorkflow>().ExecuteNextAsync(new(context.WorkDirectory, disk), Token));
    private static Task<Transcription> AttemptAsync(VideoPreparationTestContext context, Guid id)
        => context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>().Transcriptions.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.TranscriptionId == id, Token));
    private static Task<TranscriptionContent?> ContentAsync(VideoPreparationTestContext context, Guid id)
        => context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>().TranscriptionContents.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(row => row.TranscriptionId == id, Token));
    private static Task<bool> MakeEligibleAsync(VideoPreparationTestContext context, Guid id)
        => context.WithScopeAsync(async provider => { await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET next_action_at = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE transcription_id = {id}", Token); return true; });
    private static Task<bool> ExpireAsync(VideoPreparationTestContext context, Guid id)
        => context.WithScopeAsync(async provider => { await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET lease_until = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE transcription_id = {id}", Token); return true; });
    private static Task<bool> MaintainAsync(VideoPreparationTestContext context)
        => context.WithScopeAsync(async provider => { await provider.GetRequiredService<ITranscriptionWorkflow>().MaintainAsync(context.WorkDirectory, Token); return true; });
    private static string AudioPrefix(VideoPreparationTestContext.TestVideo video) => $"{video.TenantId:D}/{video.VideoId:D}/transcription";
    private static async Task AssertCleanAsync(VideoPreparationTestContext context, VideoPreparationTestContext.TestVideo video)
    {
        Assert.Empty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));
        var root = Path.Combine(context.WorkDirectory, "transcription");
        Assert.True(!Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any());
        Assert.Empty(Directory.EnumerateFiles(context.WorkDirectory, "key.bin", SearchOption.AllDirectories));
    }
}
