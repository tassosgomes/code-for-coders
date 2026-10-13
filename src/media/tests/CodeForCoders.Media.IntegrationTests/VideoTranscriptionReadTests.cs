using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoTranscriptionReadTests(VideoLibraryApiFactory factory)
{
    private const string CurrentText = "Private classroom text <script>alert('literal')</script>";

    [Fact]
    public async Task TeacherReadsCurrentContentWithoutPlaybackOrAccessDecision()
    {
        var video = await SeedVideoAsync();
        var attempt = await SeedCurrentAsync(video);
        using var response = await ReadAsync(video);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = await JsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(video.VideoId, root.GetProperty("videoId").GetGuid());
        Assert.Equal("tr_" + attempt.TranscriptionId.ToString("N")[^8..], root.GetProperty("revision").GetString());
        Assert.Equal("pt-BR", root.GetProperty("language").GetString());
        Assert.Equal(10_000, root.GetProperty("durationMs").GetInt64());
        Assert.True(Math.Abs((attempt.ReadyAt!.Value - root.GetProperty("readyAt").GetDateTimeOffset()).TotalMilliseconds) < 1);
        var segments = root.GetProperty("segments");
        Assert.Equal(2, segments.GetArrayLength());
        Assert.Equal(0, segments[0].GetProperty("startMs").GetInt64());
        Assert.Equal(4100, segments[0].GetProperty("endMs").GetInt64());
        Assert.Equal(CurrentText, segments[0].GetProperty("text").GetString());
        Assert.Equal(4100, segments[1].GetProperty("startMs").GetInt64());
        Assert.False(factory.PlaybackDecisionCalls.ContainsKey(video.TenantId));
        await using var scope = Scope(video.TenantId);
        Assert.Empty(await Db(scope).PlaybackSessions.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Db(scope).OutboxMessages.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("queued")]
    [InlineData("failed")]
    public async Task NoCurrentContentReturnsNotReady(string state)
    {
        var video = await SeedVideoAsync();
        if (state != "none") await SeedAttemptAsync(video, state);
        using var response = await ReadAsync(video);
        await AssertProblemAsync(response, HttpStatusCode.Conflict, "TRANSCRIPTION_NOT_READY");
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task MissingPermissionCannotReadReadyText()
    {
        var video = await SeedVideoAsync(); await SeedCurrentAsync(video);
        using var response = await ReadAsync(video, permission: "courses.read");
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        Assert.DoesNotContain(CurrentText, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForeignSchoolAndMissingVideoHaveTheSameNotFoundCode()
    {
        var video = await SeedVideoAsync(); await SeedCurrentAsync(video);
        using var foreign = await ReadAsync(video, tenant: Guid.CreateVersion7());
        using var missing = await ReadAsync(video, videoId: Guid.CreateVersion7());
        await AssertProblemAsync(foreign, HttpStatusCode.NotFound, "VIDEO_NOT_FOUND");
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, "VIDEO_NOT_FOUND");
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("failed")]
    public async Task RestartKeepsTheCurrentRevisionReadable(string restartState)
    {
        var video = await SeedVideoAsync(); await SeedCurrentAsync(video);
        using var before = await ReadAsync(video);
        var expected = await before.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await SeedAttemptAsync(video, restartState);
        using var during = await ReadAsync(video);
        Assert.Equal(HttpStatusCode.OK, during.StatusCode);
        Assert.Equal(expected, await during.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplacementReadsNewRevisionAndRetiresOldText()
    {
        var video = await SeedVideoAsync(); var previous = await SeedCurrentAsync(video);
        await using (var scope = Scope(video.TenantId))
        {
            var db = Db(scope);
            var attempt = await db.Transcriptions.SingleAsync(row => row.TranscriptionId == previous.TranscriptionId, TestContext.Current.CancellationToken);
            attempt.Retire(); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var current = await SeedCurrentAsync(video, "Replacement content");
        using var response = await ReadAsync(video);
        using var json = await JsonAsync(response);
        Assert.Equal("tr_" + current.TranscriptionId.ToString("N")[^8..], json.RootElement.GetProperty("revision").GetString());
        Assert.Equal("Replacement content", json.RootElement.GetProperty("segments")[0].GetProperty("text").GetString());
        Assert.DoesNotContain(CurrentText, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisabledTranscriptionDoesNotExposeStoredContent()
    {
        var video = await SeedVideoAsync(); await SeedCurrentAsync(video);
        using var disabled = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.PostConfigure<TranscriptionOptions>(options => options.Enabled = false)));
        using var client = disabled.CreateClient();
        using var request = Request(video.VideoId, video.TenantId);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.Conflict, "TRANSCRIPTION_NOT_READY");
    }

    [Fact]
    public async Task ClassroomTextStaysOutOfCapturedLogsAndRequestUrl()
    {
        var video = await SeedVideoAsync(); await SeedCurrentAsync(video);
        using var logs = new CapturedLogProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient();
        using var request = Request(video.VideoId, video.TenantId);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonAsync(response);
        Assert.Equal(CurrentText, json.RootElement.GetProperty("segments")[0].GetProperty("text").GetString());
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(CurrentText, StringComparison.Ordinal));
        Assert.DoesNotContain("Private classroom", request.RequestUri!.ToString());
        Assert.Equal(string.Empty, request.RequestUri.Query);
    }

    private async Task<Video> SeedVideoAsync()
    {
        var video = Video.Create(Guid.CreateVersion7(), "Read-only lesson", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        var lease = Guid.CreateVersion7(); video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
        video.MarkReady(lease, 10, 1024, new byte[32], "test-key");
        await using var scope = Scope(video.TenantId);
        Db(scope).Videos.Add(video); await Db(scope).SaveChangesAsync(TestContext.Current.CancellationToken);
        return video;
    }

    private async Task<Transcription> SeedCurrentAsync(Video video, string text = CurrentText)
    {
        await using var scope = Scope(video.TenantId);
        var db = Db(scope);
        var attempt = NewAttempt(video);
        db.Transcriptions.Add(attempt); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET status = 'working' WHERE transcription_id = {attempt.TranscriptionId}", TestContext.Current.CancellationToken);
        await db.Entry(attempt).ReloadAsync(TestContext.Current.CancellationToken);
        var content = TranscriptionContent.Create(new(attempt.TranscriptionId, video.TenantId, "pt-BR", 10_000,
            [new(0, 4100, text), new(4100, 7900, "Second segment")]));
        attempt.MarkReady(content, DateTimeOffset.UtcNow);
        db.TranscriptionContents.Add(content); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return attempt;
    }

    private async Task SeedAttemptAsync(Video video, string state)
    {
        await using var scope = Scope(video.TenantId);
        var attempt = NewAttempt(video);
        if (state == "failed") attempt.MarkFailed("processing-failed");
        Db(scope).Transcriptions.Add(attempt); await Db(scope).SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Transcription NewAttempt(Video video)
        => Transcription.Create(new(video.TenantId, video.VideoId, "requested", Guid.CreateVersion7(), DateTimeOffset.UtcNow));
    private AsyncServiceScope Scope(Guid tenant)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant); return scope;
    }
    private static MediaDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<MediaDbContext>();
    private HttpRequestMessage Request(Guid videoId, Guid tenant, string permission = "midia.enviar")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/internal/v1/videos/{videoId:D}/transcription");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(tenant, permissions: [permission]));
        return request;
    }
    private async Task<HttpResponseMessage> ReadAsync(Video video, Guid? tenant = null, Guid? videoId = null, string permission = "midia.enviar")
    {
        using var client = factory.CreateClient();
        using var request = Request(videoId ?? video.VideoId, tenant ?? video.TenantId, permission);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = await JsonAsync(response); Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    private sealed class CapturedLogProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturedLogger(Entries);
        public void Dispose() { }
        private sealed class CapturedLogger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Enqueue(formatter(state, exception));
        }
    }
}
