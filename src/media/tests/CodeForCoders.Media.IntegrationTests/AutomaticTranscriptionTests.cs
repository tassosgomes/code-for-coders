using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Contracts;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class AutomaticTranscriptionTests(VideoLibraryApiFactory factory)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(PreparationQueuesOneAutomaticAttemptWithoutCallingTheService))]
    [Trait("Layer", "Media automatic transcription - Integration")]
    public async Task PreparationQueuesOneAutomaticAttemptWithoutCallingTheService()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await CreateContextAsync(server);
        var video = await PrepareAsync(context);

        await AssertReadyAsync(context, video);
        var attempt = Assert.Single(await AttemptsAsync(context));
        Assert.Equal(video.TenantId, attempt.TenantId);
        Assert.Equal(video.VideoId, attempt.VideoId);
        Assert.Equal("queued", attempt.Status);
        Assert.Equal("automatic", attempt.Origin);
        Assert.Null(attempt.RequestedByAccountId);
        Assert.Empty(server.Submissions);
        await context.WithScopeAsync(async provider =>
        {
            var message = await Db(provider).OutboxMessages.IgnoreQueryFilters().SingleAsync(Token);
            Assert.Equal("midia.ativo-pronto.v1", message.RoutingKey);
            Assert.Equal(message.OccurredOn, attempt.RequestedAt);
            return true;
        });
    }

    [Fact(DisplayName = nameof(DuplicatePreparationAndRepeatedReadyFactDoNotQueueAnotherAttempt))]
    [Trait("Layer", "Media automatic transcription - Integration")]
    public async Task DuplicatePreparationAndRepeatedReadyFactDoNotQueueAnotherAttempt()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await CreateContextAsync(server);
        var video = await PrepareAsync(context);
        Assert.False(await context.ExecuteNextAsync(long.MaxValue / 4, Token));
        var message = await context.WithScopeAsync(provider => Db(provider).OutboxMessages.IgnoreQueryFilters().SingleAsync(Token));
        Assert.Equal(2, (await context.PublishRepeatedlyAsync(message, 2, Token)).Count);
        await context.WithScopeAsync(async provider =>
        {
            var repository = provider.GetRequiredService<IVideoTranscriptionRepository>();
            await repository.WithAutomaticEnqueueAsync(NewAttempt(video), provider.GetRequiredService<IUnitOfWork>().CommitAsync, Token);
            return true;
        });
        Assert.Single(await AttemptsAsync(context));
        Assert.Empty(server.Submissions);
    }

    [Fact(DisplayName = nameof(FailedPreparationNeverQueuesTranscription))]
    [Trait("Layer", "Media automatic transcription - Integration")]
    public async Task FailedPreparationNeverQueuesTranscription()
    {
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, Token, new(TranscriptionEnabled: true));
        var video = await context.CreateQueuedVideoWithBytesAsync(Encoding.UTF8.GetBytes("invalid video"), Token);
        Assert.True(await context.ExecuteNextAsync(long.MaxValue / 4, Token));
        Assert.Equal("failed", (await ReadVideoAsync(context, video)).Status);
        Assert.Empty(await AttemptsAsync(context));
    }

    [Fact(DisplayName = nameof(DisabledTranscriptionLeavesPreparationReadyWithoutAnAttempt))]
    [Trait("Layer", "Media automatic transcription - Integration")]
    public async Task DisabledTranscriptionLeavesPreparationReadyWithoutAnAttempt()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, Token,
            new(TranscriptionServiceAddress: server.BaseAddress, TranscriptionEnabled: false));
        var video = await PrepareAsync(context);
        await AssertReadyAsync(context, video);
        Assert.Empty(await AttemptsAsync(context));
        Assert.Empty(server.Submissions);
    }

    [Fact(DisplayName = nameof(UnavailableTranscriptionServiceCannotChangeReadyVideoOrPlaybackArtifacts))]
    [Trait("Layer", "Media automatic transcription - Integration")]
    public async Task UnavailableTranscriptionServiceCannotChangeReadyVideoOrPlaybackArtifacts()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        server.SubmitStatusCode = 503;
        await using var context = await CreateContextAsync(server);
        var timeToReady = new ConcurrentQueue<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, capture) =>
        {
            if (instrument.Meter == MediaTelemetry.Meter && instrument.Name == "media.videos.time_to_ready")
                capture.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => timeToReady.Enqueue(value));
        listener.Start();
        var video = await PrepareAsync(context);
        await AssertReadyAsync(context, video);
        var preparationDuration = Assert.Single(timeToReady);
        Assert.True(preparationDuration >= 0);
        Assert.Empty(server.Submissions);
        Assert.True(await context.WithScopeAsync(provider => provider.GetRequiredService<ITranscriptionWorkflow>()
            .ExecuteNextAsync(new(context.WorkDirectory, long.MaxValue / 4), Token)));
        Assert.Single(server.Submissions);
        Assert.Equal("failed", Assert.Single(await AttemptsAsync(context)).Status);
        await AssertReadyAsync(context, video);
        Assert.Equal(preparationDuration, Assert.Single(timeToReady));
    }

    [Fact(DisplayName = nameof(RejectedAutomaticInsertStillCommitsReadyAndOutbox))]
    [Trait("Layer", "Media automatic transcription - Integration")]
    public async Task RejectedAutomaticInsertStillCommitsReadyAndOutbox()
    {
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, Token, new(TranscriptionEnabled: true));
        await ExecuteSqlAsync(context, "ALTER TABLE media_access.video_transcriptions ADD CONSTRAINT test_reject_automatic CHECK (origin <> 'automatic')");
        try
        {
            var video = await PrepareAsync(context);
            await AssertReadyAsync(context, video);
            Assert.Empty(await AttemptsAsync(context));
            Assert.Contains(context.Logs.Entries, entry => entry.Contains("video preparation will still complete", StringComparison.Ordinal));
        }
        finally
        {
            await ExecuteSqlAsync(context, "ALTER TABLE media_access.video_transcriptions DROP CONSTRAINT test_reject_automatic");
        }
    }

    [Fact(DisplayName = nameof(FailedReadyOutboxCommitRollsBackTheAutomaticAttemptAndVideo))]
    [Trait("Layer", "Media automatic transcription - Integration")]
    public async Task FailedReadyOutboxCommitRollsBackTheAutomaticAttemptAndVideo()
    {
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, Token, new(TranscriptionEnabled: true));
        var video = await context.CreateDatabaseOnlyVideoAsync(1024, Token);
        await ExecuteSqlAsync(context, "ALTER TABLE media_access.outbox_messages ADD CONSTRAINT test_reject_ready CHECK (routing_key <> 'midia.ativo-pronto.v1')");
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => context.WithScopeAsync(async provider =>
            {
                await StageReadyAsync(provider, video);
                await provider.GetRequiredService<IVideoTranscriptionRepository>().WithAutomaticEnqueueAsync(
                    NewAttempt(video), provider.GetRequiredService<IUnitOfWork>().CommitAsync, Token);
                return true;
            }));
            Assert.Equal("received", (await ReadVideoAsync(context, video)).Status);
            Assert.Empty(await AttemptsAsync(context));
            Assert.False(await context.WithScopeAsync(provider => Db(provider).OutboxMessages.IgnoreQueryFilters().AnyAsync(Token)));
        }
        finally
        {
            await ExecuteSqlAsync(context, "ALTER TABLE media_access.outbox_messages DROP CONSTRAINT test_reject_ready");
        }
    }

    [Fact(DisplayName = nameof(ReadyOutboxAndAttemptBecomeVisibleOnlyAfterTheSameTransactionCommits))]
    [Trait("Layer", "Media automatic transcription - Integration")]
    public async Task ReadyOutboxAndAttemptBecomeVisibleOnlyAfterTheSameTransactionCommits()
    {
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, Token, new(TranscriptionEnabled: true));
        var video = await context.CreateDatabaseOnlyVideoAsync(1024, Token);
        await context.WithScopeAsync(async provider =>
        {
            await StageReadyAsync(provider, video);
            await provider.GetRequiredService<IVideoTranscriptionRepository>().WithAutomaticEnqueueAsync(NewAttempt(video), async token =>
            {
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(token);
                Assert.Equal("received", (await ReadVideoAsync(context, video)).Status);
                Assert.Empty(await AttemptsAsync(context));
                Assert.False(await context.WithScopeAsync(observer => Db(observer).OutboxMessages.IgnoreQueryFilters().AnyAsync(Token)));
            }, Token);
            return true;
        });
        Assert.Equal("ready", (await ReadVideoAsync(context, video)).Status);
        Assert.Single(await AttemptsAsync(context));
        Assert.True(await context.WithScopeAsync(provider => Db(provider).OutboxMessages.IgnoreQueryFilters().AnyAsync(Token)));
    }

    private Task<VideoPreparationTestContext> CreateContextAsync(TranscriptionHttpSimulator server)
        => VideoPreparationTestContext.CreateAsync(factory, Token,
            new(IncludeAudio: true, TranscriptionServiceAddress: server.BaseAddress, TranscriptionEnabled: true));

    private static async Task<VideoPreparationTestContext.TestVideo> PrepareAsync(VideoPreparationTestContext context)
    {
        var video = await context.CreateQueuedVideoAsync(640, 480, 1, Token);
        Assert.True(await context.ExecuteNextAsync(long.MaxValue / 4, Token));
        return video;
    }

    private static async Task AssertReadyAsync(VideoPreparationTestContext context, VideoPreparationTestContext.TestVideo video)
    {
        var ready = await ReadVideoAsync(context, video);
        Assert.Equal("ready", ready.Status);
        Assert.NotNull(ready.EncryptedVideoKey);
        Assert.NotNull(ready.MasterKeyId);
        var keys = await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}/hls", Token);
        Assert.Contains(keys, key => key.EndsWith("/master.m3u8", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.EndsWith(".ts", StringComparison.Ordinal));
        Assert.True(await context.WithScopeAsync(provider => Db(provider).OutboxMessages.IgnoreQueryFilters()
            .AnyAsync(message => message.RoutingKey == "midia.ativo-pronto.v1" && message.TenantId == video.TenantId, Token)));
    }

    private static async Task StageReadyAsync(IServiceProvider provider, VideoPreparationTestContext.TestVideo video)
    {
        var entity = await Db(provider).Videos.IgnoreQueryFilters().SingleAsync(row => row.VideoId == video.VideoId, Token);
        var now = DateTimeOffset.UtcNow;
        var leaseId = Guid.CreateVersion7();
        entity.MarkPreparing(leaseId, now.AddMinutes(5));
        entity.MarkReady(leaseId, 1, 1024, new byte[45], VideoPreparationTestContext.MasterKeyId);
        var eventId = Guid.CreateVersion7();
        await provider.GetRequiredService<IOutboxMessageWriter>().AppendAsync(new(eventId, video.TenantId,
            "AtivoProntoV1", "midia.ativo-pronto.v1", new AtivoProntoV1(eventId, video.TenantId, video.VideoId, now, 1), now, null), Token);
    }

    private static Transcription NewAttempt(VideoPreparationTestContext.TestVideo video)
        => Transcription.Create(new(video.TenantId, video.VideoId, "automatic", null, DateTimeOffset.UtcNow));
    private static MediaDbContext Db(IServiceProvider provider) => provider.GetRequiredService<MediaDbContext>();
    private static Task<Video> ReadVideoAsync(VideoPreparationTestContext context, VideoPreparationTestContext.TestVideo video)
        => context.WithScopeAsync(provider => Db(provider).Videos.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.VideoId == video.VideoId, Token));
    private static Task<Transcription[]> AttemptsAsync(VideoPreparationTestContext context)
        => context.WithScopeAsync(provider => Db(provider).Transcriptions.IgnoreQueryFilters().AsNoTracking().ToArrayAsync(Token));
    private static Task<int> ExecuteSqlAsync(VideoPreparationTestContext context, string sql)
        => context.WithScopeAsync(provider => Db(provider).Database.ExecuteSqlRawAsync(sql, Token));
}
