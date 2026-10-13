using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CodeForCoders.Media.IntegrationTests.TranscriptionTestHarness;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class TranscriptionRestartTests(VideoLibraryApiFactory factory) : IDisposable
{
    // The API must read the database where the worker context writes its rows.
    private readonly Lazy<HttpClient> api = new(() => factory.WithWebHostBuilder(builder =>
        builder.UseSetting("ConnectionStrings:DefaultConnection", factory.VideoPreparationConnectionString)).CreateClient());

    public void Dispose()
    {
        if (api.IsValueCreated) api.Value.Dispose();
    }

    private const string NewResult = """{"schemaVersion":1,"jobId":"trn_test","language":"pt-BR","durationMs":1000,"segments":[{"startMs":0,"endMs":1000,"text":" refeita "}]}""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartPreservesTerminalAudioCleanupAcrossStorageFailureAndInterruption(bool afterDeletion)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await CreateContextAsync(factory, server);
        var (video, current) = await ReadyAsync(context, server);
        using var accepted = await RestartAsync(video);
        var failed = (await AttemptsAsync(context, video)).Single(row => row.Status == "queued").TranscriptionId;
        server.Status = "downloading";
        Assert.True(await ExecuteAsync(context));
        await FailBeforeCleanupAsync(context, failed);
        Assert.Null((await AttemptAsync(context, failed)).AudioDeletedAt);
        Assert.NotEmpty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));

        using var restarted = await RestartAsync(video);
        Assert.Equal(HttpStatusCode.Accepted, restarted.StatusCode);
        Assert.Null(await FindAttemptAsync(context, failed));
        await AssertPendingCleanupAsync(context, failed, expected: true);

        context.FailNextStorageDelete(afterDeletion);
        await Assert.ThrowsAsync<StorageUnavailableException>(() => MaintainAsync(context));
        await AssertPendingCleanupAsync(context, failed, expected: true);
        var keys = await context.ListObjectKeysAsync(AudioPrefix(video), Token);
        if (afterDeletion) Assert.Empty(keys); else Assert.NotEmpty(keys);

        // A new scope recovers the durable intent, including a crash after S3 deleted the object.
        await MaintainAsync(context);
        await AssertPendingCleanupAsync(context, failed, expected: false);
        Assert.Empty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));
        Assert.True((await AttemptAsync(context, current)).IsCurrent);
        Assert.NotNull(await ContentAsync(context, current));
        using var read = await GetVideoAsync(video);
        using var json = await ReadAsync(read);
        AssertState(json.RootElement, "in-progress", readable: true, canStart: false);
    }

    [Fact]
    public async Task RolledBackRestartKeepsTheFailedAttemptAndItsCleanupResponsibility()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var context = await CreateContextAsync(factory, server);
        var (video, failed) = await EnqueuePreparedAsync(context);
        Assert.True(await ExecuteAsync(context));
        await FailBeforeCleanupAsync(context, failed);
        await context.WithScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<MediaDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Token);
            await provider.GetRequiredService<IVideoTranscriptionRepository>().EnqueueAsync(
                Transcription.Create(new(video.TenantId, video.VideoId, "requested", Guid.CreateVersion7(), DateTimeOffset.UtcNow)), Token);
            Assert.False(await db.Transcriptions.IgnoreQueryFilters().AnyAsync(row => row.TranscriptionId == failed, Token));
            Assert.True(await db.TranscriptionAudioCleanups.IgnoreQueryFilters().AnyAsync(row => row.TranscriptionId == failed, Token));
            await transaction.RollbackAsync(Token);
            return true;
        });
        Assert.Equal("failed", (await AttemptAsync(context, failed)).Status);
        Assert.Single(await AttemptsAsync(context, video));
        await AssertPendingCleanupAsync(context, failed, expected: false);
        await MaintainAsync(context);
        Assert.Empty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));
        Assert.NotNull((await AttemptAsync(context, failed)).AudioDeletedAt);
    }

    private static async Task FailBeforeCleanupAsync(VideoPreparationTestContext context, Guid id)
    {
        await MakeEligibleAsync(context, id);
        await context.WithScopeAsync(async provider =>
        {
            var repository = provider.GetRequiredService<ITranscriptionWorkerRepository>();
            var claimed = (await repository.ClaimNextAsync(new(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), true), Token))!;
            Assert.Equal(id, claimed.Attempt.TranscriptionId);
            Assert.True(await repository.WithLeaseAsync(claimed, async (attempt, token) =>
            {
                // Model a worker interruption after its terminal commit, before deleting the audio.
                attempt.MarkFailed("timed-out");
                await provider.GetRequiredService<IUnitOfWork>().CommitAsync(token);
            }, Token));
            return true;
        });
    }

    private static Task<bool> AssertPendingCleanupAsync(VideoPreparationTestContext context, Guid id, bool expected)
        => context.WithScopeAsync(async provider =>
        {
            var cleanups = await provider.GetRequiredService<ITranscriptionWorkerRepository>().GetPendingAudioCleanupAsync(Token);
            Assert.Equal(expected, cleanups.Any(row => row.TranscriptionId == id));
            return true;
        });

    private static Task<bool> MaintainAsync(VideoPreparationTestContext context)
        => context.WithScopeAsync(async provider =>
        {
            await provider.GetRequiredService<ITranscriptionWorkflow>().MaintainAsync(context.WorkDirectory, Token);
            return true;
        });

    [Fact]
    public async Task RestartOverReadyTextStartsANewAttemptAndLeavesTheCurrentOneReadable()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, first) = await ReadyAsync(context, server);
        using var response = await RestartAsync(video);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var json = await ReadAsync(response); AssertState(json.RootElement, "in-progress", readable: true, canStart: false);
        Assert.True((await AttemptAsync(context, first)).IsCurrent); Assert.NotNull(await ContentAsync(context, first));
        var attempts = await AttemptsAsync(context, video);
        Assert.Equal(2, attempts.Count); Assert.Single(attempts, row => row.Status == "queued" && !row.IsCurrent);
        using var read = await GetVideoAsync(video); using var state = await ReadAsync(read); AssertState(state.RootElement, "in-progress", true, false);
    }

    [Fact]
    public async Task ReadyRestartSwapsTheCurrentTextInOneStepAndErasesTheOldContent()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, first) = await ReadyAsync(context, server);
        using var accepted = await RestartAsync(video); Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        server.Result = NewResult; var second = (await AttemptsAsync(context, video)).Single(row => row.Status == "queued").TranscriptionId;
        await FinishAsync(context, server, second);
        Assert.False((await AttemptAsync(context, first)).IsCurrent); Assert.Null(await ContentAsync(context, first));
        var current = await AttemptAsync(context, second);
        Assert.True(current.IsCurrent); Assert.Equal("ready", current.Status);
        using var json = JsonDocument.Parse((await ContentAsync(context, second))!.SegmentsJson); Assert.Equal("refeita", json.RootElement[0].GetProperty("text").GetString());
        Assert.Single(await AttemptsAsync(context, video), row => row.IsCurrent);
        using var read = await GetVideoAsync(video); using var state = await ReadAsync(read); AssertState(state.RootElement, "ready", true, true);
    }

    [Fact]
    public async Task RestartThatFailsLeavesTheCurrentTextIntactAndReportsFailedButReadable()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, first) = await ReadyAsync(context, server);
        using var accepted = await RestartAsync(video); var second = (await AttemptsAsync(context, video)).Single(row => row.Status == "queued").TranscriptionId;
        Assert.True(await ExecuteAsync(context)); server.Status = "failed"; server.FailureCode = "TRANSCRIPTION_FAILED";
        Assert.True(await RetryAsync(context, second));
        Assert.Equal("failed", (await AttemptAsync(context, second)).Status);
        var current = await AttemptAsync(context, first); Assert.True(current.IsCurrent); Assert.NotNull(await ContentAsync(context, first));
        using var read = await GetVideoAsync(video); using var json = await ReadAsync(read);
        AssertState(json.RootElement, "failed", readable: true, canStart: true);
        Assert.Equal("processing-failed", json.RootElement.GetProperty("transcription").GetProperty("failureReason").GetString());
        Assert.Equal(current.ReadyAt, json.RootElement.GetProperty("transcription").GetProperty("readyAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task StartingAnotherAttemptErasesTheEarlierFailedOneButNeverTheCurrentText()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, first) = await ReadyAsync(context, server);
        using var accepted = await RestartAsync(video); var failed = (await AttemptsAsync(context, video)).Single(row => row.Status == "queued").TranscriptionId;
        Assert.True(await ExecuteAsync(context)); server.Status = "failed"; Assert.True(await RetryAsync(context, failed));
        Assert.Equal("failed", (await AttemptAsync(context, failed)).Status);
        server.Status = "downloading"; server.FailureCode = null;
        using var again = await RestartAsync(video); Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        Assert.Null(await FindAttemptAsync(context, failed));
        var attempts = await AttemptsAsync(context, video);
        Assert.Equal(2, attempts.Count); Assert.Contains(attempts, row => row.TranscriptionId == first && row.IsCurrent); Assert.Contains(attempts, row => row.Status == "queued");
        Assert.NotNull(await ContentAsync(context, first));
    }

    [Fact]
    public async Task RestartAfterAFailureWithoutAnyTextStartsAFreshAttempt()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, id) = await EnqueuePreparedAsync(context); Assert.True(await ExecuteAsync(context));
        server.Status = "failed"; server.FailureCode = "SOMETHING"; Assert.True(await RetryAsync(context, id));
        using (var read = await GetVideoAsync(video)) { using var json = await ReadAsync(read); AssertState(json.RootElement, "failed", readable: false, canStart: true); }
        using var response = await RestartAsync(video); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var state = await ReadAsync(response); AssertState(state.RootElement, "in-progress", readable: false, canStart: false);
        Assert.Null(await FindAttemptAsync(context, id)); Assert.Single(await AttemptsAsync(context, video));
    }

    [Fact]
    public async Task TwoSimultaneousRestartsCreateExactlyOneNewAttempt()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, first) = await ReadyAsync(context, server);
        var responses = await Task.WhenAll(RestartAsync(video), RestartAsync(video), RestartAsync(video));
        foreach (var response in responses)
        {
            using (response) { Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); using var json = await ReadAsync(response); AssertState(json.RootElement, "in-progress", true, false); }
        }
        var attempts = await AttemptsAsync(context, video);
        Assert.Equal(2, attempts.Count); Assert.Single(attempts, row => row.Status == "queued"); Assert.Single(attempts, row => row.TranscriptionId == first && row.IsCurrent);
    }

    [Fact]
    public async Task FailureWhileSwappingRollsBackAndKeepsTheCurrentTextAndItsContent()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, first) = await ReadyAsync(context, server);
        using var accepted = await RestartAsync(video); var second = (await AttemptsAsync(context, video)).Single(row => row.Status == "queued").TranscriptionId;
        TranscriptionWorkItem claimed = null!;
        await context.WithScopeAsync(async provider =>
        {
            claimed = (await provider.GetRequiredService<ITranscriptionWorkerRepository>().ClaimNextAsync(new(DateTimeOffset.UtcNow.AddMinutes(1), TimeSpan.FromMinutes(5), true), Token))!; return true;
        });
        var content = TranscriptionResultValidation.Validate(second, video.TenantId, Encoding.UTF8.GetBytes(NewResult));
        await context.WithScopeAsync(async provider =>
        {
            var repository = provider.GetRequiredService<ITranscriptionWorkerRepository>(); var db = provider.GetRequiredService<MediaDbContext>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.WithLeaseAsync(claimed, async (attempt, token) =>
            {
                await repository.ReplaceContentAsync(content, token); attempt.MarkReady(content, DateTimeOffset.UtcNow); await db.SaveChangesAsync(token);
                throw new InvalidOperationException("The swap broke after both changes were written.");
            }, Token));
            return true;
        });
        var current = await AttemptAsync(context, first); Assert.True(current.IsCurrent); Assert.NotNull(await ContentAsync(context, first));
        Assert.Null(await ContentAsync(context, second)); Assert.False((await AttemptAsync(context, second)).IsCurrent);
        using var read = await GetVideoAsync(video); using var json = await ReadAsync(read); Assert.True(json.RootElement.GetProperty("transcription").GetProperty("readable").GetBoolean());
    }

    [Fact]
    public async Task WorkerThatCannotCommitTheSwapFailsTheNewAttemptAndLeavesTheCurrentOneUntouched()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, first) = await ReadyAsync(context, server);
        using var accepted = await RestartAsync(video); var second = (await AttemptsAsync(context, video)).Single(row => row.Status == "queued").TranscriptionId;
        Assert.True(await ExecuteAsync(context));
        // A leftover content row for the new attempt makes the commit of the swap collide on its primary key.
        await context.WithScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<MediaDbContext>();
            db.TranscriptionContents.Add(TranscriptionResultValidation.Validate(second, video.TenantId, Encoding.UTF8.GetBytes(NewResult))); await db.SaveChangesAsync(Token); return true;
        });
        server.Status = "completed"; Assert.True(await RetryAsync(context, second));
        var failed = await AttemptAsync(context, second); Assert.Equal("failed", failed.Status); Assert.Equal("processing-failed", failed.FailureReason); Assert.False(failed.IsCurrent);
        var current = await AttemptAsync(context, first); Assert.True(current.IsCurrent); Assert.Equal("ready", current.Status); Assert.NotNull(await ContentAsync(context, first));
        using var read = await GetVideoAsync(video); using var json = await ReadAsync(read); AssertState(json.RootElement, "failed", true, true);
    }

    [Theory]
    [InlineData("MEDIA_DURATION_LIMIT_EXCEEDED", "duration-limit")]
    [InlineData("MEDIA_SIZE_LIMIT_EXCEEDED", "size-limit")]
    public async Task LimitFailureOfARestartShowsItsReasonAndRefusesAnotherRestart(string code, string reason)
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, first) = await ReadyAsync(context, server);
        using var accepted = await RestartAsync(video); var second = (await AttemptsAsync(context, video)).Single(row => row.Status == "queued").TranscriptionId;
        Assert.True(await ExecuteAsync(context)); server.Status = "failed"; server.FailureCode = code; Assert.True(await RetryAsync(context, second));
        using (var read = await GetVideoAsync(video))
        {
            using var json = await ReadAsync(read); AssertState(json.RootElement, "failed", readable: true, canStart: false);
            Assert.Equal(reason, json.RootElement.GetProperty("transcription").GetProperty("failureReason").GetString());
        }
        using var refused = await RestartAsync(video);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode); using var problem = await ReadAsync(refused);
        Assert.Equal("TRANSCRIPTION_NOT_RETRYABLE", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(2, (await AttemptsAsync(context, video)).Count); Assert.True((await AttemptAsync(context, first)).IsCurrent);
    }

    [Fact]
    public async Task ConsecutiveRestartsAlwaysLeaveOneCurrentTextWithOneContent()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); await using var context = await CreateContextAsync(factory, server);
        var (video, _) = await ReadyAsync(context, server);
        for (var round = 0; round < 2; round++)
        {
            using var accepted = await RestartAsync(video); Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            var next = (await AttemptsAsync(context, video)).Single(row => row.Status == "queued").TranscriptionId;
            await FinishAsync(context, server, next);
        }
        var attempts = await AttemptsAsync(context, video);
        Assert.Single(attempts, row => row.IsCurrent);
        var ids = attempts.Select(row => row.TranscriptionId).ToArray();
        var contents = await context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>().TranscriptionContents.IgnoreQueryFilters()
            .CountAsync(row => ids.Contains(row.TranscriptionId), Token));
        Assert.Equal(1, contents);
    }

    private static async Task<(VideoPreparationTestContext.TestVideo Video, Guid Id)> ReadyAsync(VideoPreparationTestContext context, TranscriptionHttpSimulator server)
    {
        var (video, id) = await EnqueuePreparedAsync(context);
        await FinishAsync(context, server, id);
        return (video, id);
    }

    private static async Task FinishAsync(VideoPreparationTestContext context, TranscriptionHttpSimulator server, Guid id)
    {
        server.Status = "downloading"; Assert.True(await ExecuteAsync(context));
        server.Status = "completed"; Assert.True(await RetryAsync(context, id));
        Assert.Equal("ready", (await AttemptAsync(context, id)).Status);
    }

    private static Task<IReadOnlyList<Transcription>> AttemptsAsync(VideoPreparationTestContext context, VideoPreparationTestContext.TestVideo video)
        => context.WithScopeAsync<IReadOnlyList<Transcription>>(async provider => await provider.GetRequiredService<MediaDbContext>().Transcriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.VideoId == video.VideoId).OrderBy(row => row.RequestedAt).ToArrayAsync(Token));

    private async Task<HttpResponseMessage> RestartAsync(VideoPreparationTestContext.TestVideo video)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/v1/videos/{video.VideoId}/transcription-requests");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateTokenForActor(video.TenantId, Guid.CreateVersion7(), "midia.enviar"));
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        return await api.Value.SendAsync(request, Token);
    }

    private async Task<HttpResponseMessage> GetVideoAsync(VideoPreparationTestContext.TestVideo video)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/internal/v1/videos/{video.VideoId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateTokenForActor(video.TenantId, Guid.CreateVersion7(), "midia.enviar"));
        return await api.Value.SendAsync(request, Token);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Token), cancellationToken: Token);

    private static void AssertState(JsonElement video, string status, bool readable, bool canStart)
    {
        var state = video.GetProperty("transcription");
        Assert.Equal(status, state.GetProperty("status").GetString()); Assert.Equal(readable, state.GetProperty("readable").GetBoolean()); Assert.Equal(canStart, state.GetProperty("canStart").GetBoolean());
    }
}
