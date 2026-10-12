using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoTranscriptionRequestTests(VideoLibraryApiFactory factory)
{
    [Fact]
    public async Task RequestQueuesOneAttemptWithRequesterAndPreservesVideo()
    {
        var video = await SeedAsync(); var actor = Guid.CreateVersion7();
        using var response = await PostAsync(video, actor: actor);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal($"/internal/v1/videos/{video.VideoId:D}", response.Headers.Location?.OriginalString);
        using var json = await ReadAsync(response); AssertState(json.RootElement, "in-progress", false);
        await using var scope = Scope(video.TenantId); var db = Db(scope);
        var attempt = await db.Transcriptions.SingleAsync(row => row.VideoId == video.VideoId, TestContext.Current.CancellationToken);
        Assert.Equal("queued", attempt.Status); Assert.Equal("requested", attempt.Origin); Assert.Equal(actor, attempt.RequestedByAccountId);
        Assert.False(attempt.IsCurrent);
        var persisted = await db.Videos.SingleAsync(row => row.VideoId == video.VideoId, TestContext.Current.CancellationToken);
        Assert.Equal("ready", persisted.Status); Assert.Equal(video.Title, persisted.Title); Assert.Equal(video.EncryptedVideoKey, persisted.EncryptedVideoKey);
        Assert.Empty(await db.OutboxMessages.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TwoActorsRequestingConcurrentlyCreateOneAttempt()
    {
        var video = await SeedAsync();
        var responses = await Task.WhenAll(PostAsync(video), PostAsync(video));
        foreach (var response in responses) { using (response) { Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); using var json = await ReadAsync(response); AssertState(json.RootElement, "in-progress", false); } }
        await using var scope = Scope(video.TenantId);
        Assert.Equal(1, await Db(scope).Transcriptions.CountAsync(row => row.VideoId == video.VideoId, TestContext.Current.CancellationToken));
        Assert.Equal(2, await Db(scope).OperationIdempotencyRecords.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OngoingRequestIsAcceptedWithoutAnotherAttempt()
    {
        var video = await SeedAsync(); using var first = await PostAsync(video); using var second = await PostAsync(video);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        await using var scope = Scope(video.TenantId);
        Assert.Equal(1, await Db(scope).Transcriptions.CountAsync(row => row.VideoId == video.VideoId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentSameKeyCommitsOneResponse()
    {
        var video = await SeedAsync(); var actor = Guid.CreateVersion7(); var key = Guid.CreateVersion7().ToString();
        var responses = await Task.WhenAll(PostAsync(video, key, actor), PostAsync(video, key, actor));
        using var first = responses[0]; using var second = responses[1];
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode); Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await using var scope = Scope(video.TenantId);
        Assert.Equal(1, await Db(scope).OperationIdempotencyRecords.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplayRemainsIdenticalAfterStateChanges()
    {
        var video = await SeedAsync(); var actor = Guid.CreateVersion7(); var key = Guid.CreateVersion7().ToString();
        using var first = await PostAsync(video, key, actor);
        await using (var scope = Scope(video.TenantId))
        {
            var db = Db(scope); var attempt = await db.Transcriptions.SingleAsync(TestContext.Current.CancellationToken);
            attempt.MarkFailed("processing-failed"); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var replay = await PostAsync(video, key, actor);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReusedKeyForAnotherVideoIsRejected()
    {
        var first = await SeedAsync(); var second = await SeedAsync(first.TenantId); var actor = Guid.CreateVersion7(); var key = Guid.CreateVersion7().ToString();
        using var accepted = await PostAsync(first, key, actor); using var denied = await PostAsync(second, key, actor);
        await ProblemAsync(denied, HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED");
        await using var scope = Scope(first.TenantId);
        Assert.Equal(1, await Db(scope).Transcriptions.CountAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("received")]
    [InlineData("preparing")]
    [InlineData("failed")]
    public async Task NonReadyVideoIsRejected(string status)
    {
        var video = await SeedAsync(status: status); using var response = await PostAsync(video);
        await ProblemAsync(response, HttpStatusCode.Conflict, "VIDEO_NOT_READY");
        await using var scope = Scope(video.TenantId); Assert.Empty(await Db(scope).Transcriptions.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("duration-limit")]
    [InlineData("size-limit")]
    public async Task LimitFailureIsNotRetryable(string reason)
    {
        var video = await SeedAsync(); await SeedFailureAsync(video, reason); using var response = await PostAsync(video);
        await ProblemAsync(response, HttpStatusCode.Conflict, "TRANSCRIPTION_NOT_RETRYABLE");
        await using var scope = Scope(video.TenantId); Assert.Equal(1, await Db(scope).Transcriptions.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RetryableFailureQueuesANewAttempt()
    {
        var video = await SeedAsync(); await SeedFailureAsync(video, "processing-failed"); using var response = await PostAsync(video);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await using var scope = Scope(video.TenantId); Assert.Equal(2, await Db(scope).Transcriptions.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForeignSchoolAndMissingVideoReturnSameNotFound()
    {
        var video = await SeedAsync(); using var foreign = await PostAsync(video, tenant: Guid.CreateVersion7());
        await ProblemAsync(foreign, HttpStatusCode.NotFound, "VIDEO_NOT_FOUND");
        using var missing = await PostAsync(video, videoId: Guid.CreateVersion7()); await ProblemAsync(missing, HttpStatusCode.NotFound, "VIDEO_NOT_FOUND");
        await using var scope = Scope(video.TenantId); Assert.Empty(await Db(scope).Transcriptions.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ActorWithoutPermissionCannotQueue()
    {
        var video = await SeedAsync(); using var response = await PostAsync(video, permission: "courses.read");
        await ProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
    }

    [Fact]
    public async Task MissingIdempotencyKeyIsRejected()
    {
        var video = await SeedAsync(); using var client = factory.CreateClient(); using var request = Request(HttpMethod.Post, $"/internal/v1/videos/{video.VideoId}/transcription-requests", video.TenantId);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        await ProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_REQUEST");
    }

    [Fact]
    public async Task DisabledFeatureHidesExistingStateAndDoesNotQueue()
    {
        var video = await SeedAsync(); using var accepted = await PostAsync(video);
        using var disabled = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.PostConfigure<TranscriptionOptions>(options => options.Enabled = false)));
        using var client = disabled.CreateClient();
        using var request = Request(HttpMethod.Post, $"/internal/v1/videos/{video.VideoId}/transcription-requests", video.TenantId); request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        await ProblemAsync(response, HttpStatusCode.Conflict, "TRANSCRIPTION_NOT_RETRYABLE");
        using var get = Request(HttpMethod.Get, $"/internal/v1/videos/{video.VideoId}", video.TenantId); using var read = await client.SendAsync(get, TestContext.Current.CancellationToken);
        using var json = await ReadAsync(read); AssertState(json.RootElement, "not-requested", false);
        await using var scope = Scope(video.TenantId); Assert.Equal(1, await Db(scope).Transcriptions.CountAsync(TestContext.Current.CancellationToken));
        var fresh = await SeedAsync(video.TenantId);
        using var freshRequest = Request(HttpMethod.Post, $"/internal/v1/videos/{fresh.VideoId}/transcription-requests", fresh.TenantId); freshRequest.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var refused = await client.SendAsync(freshRequest, TestContext.Current.CancellationToken); await ProblemAsync(refused, HttpStatusCode.Conflict, "TRANSCRIPTION_NOT_RETRYABLE");
        Assert.Equal(1, await Db(scope).Transcriptions.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AllVideoResponsesIncludeDerivedStateAndCanonicalTags()
    {
        var video = await SeedAsync(); using var client = factory.CreateClient();
        using var initial = await client.SendAsync(Request(HttpMethod.Get, $"/internal/v1/videos/{video.VideoId}", video.TenantId), TestContext.Current.CancellationToken);
        using (var json = await ReadAsync(initial)) AssertState(json.RootElement, "not-requested", true);
        using var accepted = await PostAsync(video);
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Patch, HttpMethod.Put })
        {
            var path = $"/internal/v1/videos/{video.VideoId}" + (method == HttpMethod.Put ? "/tags" : "");
            using var request = Request(method, path, video.TenantId);
            if (method != HttpMethod.Get)
            {
                request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
                request.Content = method == HttpMethod.Put ? JsonContent.Create(new { tags = new[] { "Docker" } }) : JsonContent.Create(new { title = "Renamed" });
            }
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadAsync(response); AssertState(json.RootElement, "in-progress", false);
        }
        using var list = await client.SendAsync(Request(HttpMethod.Get, "/internal/v1/videos", video.TenantId), TestContext.Current.CancellationToken);
        using var page = await ReadAsync(list); AssertState(page.RootElement.GetProperty("data")[0], "in-progress", false);
        Assert.Equal("Docker", page.RootElement.GetProperty("data")[0].GetProperty("tags")[0].GetString());
    }

    [Fact]
    public async Task DatabasePartialIndexesGuardOngoingAndCurrentAttempts()
    {
        var video = await SeedAsync(); using var queued = await PostAsync(video);
        await using (var scope = Scope(video.TenantId))
        {
            var db = Db(scope); db.Transcriptions.Add(NewAttempt(video));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
        await using (var scope = Scope(video.TenantId))
        {
            var db = Db(scope);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET status = 'ready', is_current = true, ready_at = {DateTimeOffset.UtcNow} WHERE video_id = {video.VideoId}", TestContext.Current.CancellationToken);
            var second = NewAttempt(video); db.Transcriptions.Add(second); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET status = 'ready', is_current = true WHERE transcription_id = {second.TranscriptionId}", TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task CurrentTranscriptRemainsReadableWhileReplacementIsQueuedOrFailed()
    {
        var video = await SeedAsync(); using var first = await PostAsync(video); var readyAt = DateTimeOffset.UtcNow;
        await using (var scope = Scope(video.TenantId))
            await Db(scope).Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET status = 'ready', is_current = true, ready_at = {readyAt} WHERE video_id = {video.VideoId}", TestContext.Current.CancellationToken);
        using var client = factory.CreateClient();
        using (var read = await client.SendAsync(Request(HttpMethod.Get, $"/internal/v1/videos/{video.VideoId}", video.TenantId), TestContext.Current.CancellationToken))
        {
            using var json = await ReadAsync(read); AssertState(json.RootElement, "ready", true); Assert.True(json.RootElement.GetProperty("transcription").GetProperty("readable").GetBoolean());
        }
        using var replacement = await PostAsync(video);
        using (var json = await ReadAsync(replacement)) { AssertState(json.RootElement, "in-progress", false); Assert.True(json.RootElement.GetProperty("transcription").GetProperty("readable").GetBoolean()); }
        await using (var scope = Scope(video.TenantId))
        {
            var db = Db(scope); var attempt = await db.Transcriptions.SingleAsync(row => !row.IsCurrent, TestContext.Current.CancellationToken);
            attempt.MarkFailed("processing-failed"); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var failed = await client.SendAsync(Request(HttpMethod.Get, $"/internal/v1/videos/{video.VideoId}", video.TenantId), TestContext.Current.CancellationToken);
        using var failure = await ReadAsync(failed); AssertState(failure.RootElement, "failed", true);
        Assert.True(failure.RootElement.GetProperty("transcription").GetProperty("readable").GetBoolean());
        Assert.Equal("processing-failed", failure.RootElement.GetProperty("transcription").GetProperty("failureReason").GetString());
        Assert.True(Math.Abs((failure.RootElement.GetProperty("transcription").GetProperty("readyAt").GetDateTimeOffset() - readyAt).TotalMilliseconds) < 1);
    }

    private static Transcription NewAttempt(Video video) => Transcription.Create(new(video.TenantId, video.VideoId, "requested", Guid.CreateVersion7(), DateTimeOffset.UtcNow));
    private async Task SeedFailureAsync(Video video, string reason)
    {
        await using var scope = Scope(video.TenantId); var attempt = NewAttempt(video); attempt.MarkFailed(reason);
        Db(scope).Transcriptions.Add(attempt); await Db(scope).SaveChangesAsync(TestContext.Current.CancellationToken);
    }
    private async Task<Video> SeedAsync(Guid? tenant = null, string status = "ready")
    {
        var video = Video.Create(tenant ?? Guid.CreateVersion7(), "Original", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        if (status != "received")
        {
            var lease = Guid.CreateVersion7(); video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
            if (status == "ready") video.MarkReady(lease, 10, 1024, new byte[32], "test-key");
            else if (status == "failed") video.MarkFailed(lease, VideoFailureReasons.PreparationFailed);
        }
        await using var scope = Scope(video.TenantId); Db(scope).Videos.Add(video); await Db(scope).SaveChangesAsync(TestContext.Current.CancellationToken); return video;
    }
    private AsyncServiceScope Scope(Guid tenant)
    {
        var scope = factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant); return scope;
    }
    private static MediaDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<MediaDbContext>();
    private async Task<HttpResponseMessage> PostAsync(Video video, string? key = null, Guid? actor = null, Guid? tenant = null, string permission = "midia.enviar", Guid? videoId = null)
    {
        using var client = factory.CreateClient(); using var request = Request(HttpMethod.Post, $"/internal/v1/videos/{videoId ?? video.VideoId}/transcription-requests", tenant ?? video.TenantId, actor, permission);
        request.Headers.Add("Idempotency-Key", key ?? Guid.CreateVersion7().ToString()); return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private HttpRequestMessage Request(HttpMethod method, string path, Guid tenant, Guid? actor = null, string permission = "midia.enviar")
    {
        var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateTokenForActor(tenant, actor ?? Guid.CreateVersion7(), permission)); return request;
    }
    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); using var json = await ReadAsync(response); Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
    private static void AssertState(JsonElement video, string status, bool canStart)
    {
        var state = video.GetProperty("transcription"); Assert.Equal(status, state.GetProperty("status").GetString()); Assert.Equal(canStart, state.GetProperty("canStart").GetBoolean());
    }
}
