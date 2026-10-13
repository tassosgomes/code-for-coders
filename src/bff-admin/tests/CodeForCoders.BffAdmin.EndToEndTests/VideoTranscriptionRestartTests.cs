using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class VideoTranscriptionRestartTests(BffAdminApiFactory factory)
{
    private static readonly DateTimeOffset ReadyAt = new(2026, 10, 12, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RestartOverReadyTextReportsInProgressWhileTheCurrentTextStaysReadable()
    {
        var (client, cookie, csrf) = await TeacherAsync(); factory.VideoLibraryHandler.StatusCode = HttpStatusCode.Accepted;
        factory.VideoLibraryHandler.TranscriptionState = new { status = "in-progress", readable = true, readyAt = ReadyAt, failureReason = (string?)null, canStart = false };
        using (client) using (var request = PostRequest(Guid.CreateVersion7(), cookie, csrf))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            using var json = await ReadAsync(response); var state = json.RootElement.GetProperty("transcription");
            Assert.Equal("in-progress", state.GetProperty("status").GetString()); Assert.True(state.GetProperty("readable").GetBoolean());
            Assert.False(state.GetProperty("canStart").GetBoolean()); Assert.Equal(ReadyAt, state.GetProperty("readyAt").GetDateTimeOffset());
        }
    }

    [Theory]
    [InlineData("duration-limit", false)]
    [InlineData("size-limit", false)]
    [InlineData("service-unavailable", true)]
    [InlineData("processing-failed", true)]
    [InlineData("timed-out", true)]
    public async Task FailedRestartKeepsReasonReadabilityAndRestartPermissionUnchanged(string reason, bool canStart)
    {
        var (client, cookie, csrf) = await TeacherAsync(); factory.VideoLibraryHandler.StatusCode = HttpStatusCode.Accepted;
        factory.VideoLibraryHandler.TranscriptionState = new { status = "failed", readable = true, readyAt = ReadyAt, failureReason = reason, canStart };
        using (client) using (var request = PostRequest(Guid.CreateVersion7(), cookie, csrf))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            using var json = await ReadAsync(response); var state = json.RootElement.GetProperty("transcription");
            Assert.Equal("failed", state.GetProperty("status").GetString()); Assert.True(state.GetProperty("readable").GetBoolean());
            Assert.Equal(reason, state.GetProperty("failureReason").GetString()); Assert.Equal(canStart, state.GetProperty("canStart").GetBoolean());
        }
    }

    [Fact]
    public async Task LimitReasonRefusesRestartWithTheSameCodeAndNoMediaDetail()
    {
        var (client, cookie, csrf) = await TeacherAsync(); factory.VideoLibraryHandler.StatusCode = HttpStatusCode.Conflict; factory.VideoLibraryHandler.ProblemCode = "TRANSCRIPTION_NOT_RETRYABLE";
        using (client) using (var request = PostRequest(Guid.CreateVersion7(), cookie, csrf))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var json = await ReadAsync(response);
            Assert.Equal("TRANSCRIPTION_NOT_RETRYABLE", json.RootElement.GetProperty("code").GetString());
            Assert.True(json.RootElement.TryGetProperty("traceId", out _)); Assert.DoesNotContain("video-library-test", json.RootElement.GetRawText(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RestartOfAVideoThatIsNotReadyIsRefusedWithVideoNotReady()
    {
        var (client, cookie, csrf) = await TeacherAsync(); factory.VideoLibraryHandler.StatusCode = HttpStatusCode.Conflict; factory.VideoLibraryHandler.ProblemCode = "VIDEO_NOT_READY";
        var id = Guid.CreateVersion7();
        using (client) using (var request = PostRequest(id, cookie, csrf))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            using var json = await ReadAsync(response); Assert.Equal("VIDEO_NOT_READY", json.RootElement.GetProperty("code").GetString());
            Assert.Equal($"/internal/v1/videos/{id:D}/transcription-requests", factory.VideoLibraryHandler.LastPath);
            Assert.Equal("restart-video-transcription", factory.VideoLibraryHandler.LastIdempotencyKey);
        }
    }

    [Fact]
    public async Task RestartWithoutPermissionNeverReachesMediaEvenWhenTheTextIsCurrent()
    {
        var (client, cookie, csrf) = await TeacherAsync();
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession! with { Permissions = ["courses.read"] };
        using (client) using (var request = PostRequest(Guid.CreateVersion7(), cookie, csrf))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
        }
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);

    private async Task<(HttpClient Client, string Cookie, string Csrf)> TeacherAsync()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.VideoLibraryHandler.Reset();
        var client = factory.CreateClient();
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        loginRequest.Headers.Add("Idempotency-Key", "video-restart-login");
        using var loginResponse = await client.SendAsync(loginRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = await loginResponse.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        factory.StaffSessionIdentityHandler.ValidatedSession = new StaffSessionValidatedV1(
            session.AccountId, "Marina Alves", ["professor"], ["midia.enviar"], DateTimeOffset.UtcNow.AddMinutes(30), "teacher-media-token");
        return (client, loginResponse.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0], session.CsrfToken);
    }

    private static HttpRequestMessage PostRequest(Guid videoId, string cookie, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/videos/{videoId:D}/transcription-requests");
        request.Headers.Add("Cookie", cookie); request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("X-CSRF-Token", csrf); request.Headers.Add("Idempotency-Key", "restart-video-transcription"); return request;
    }
}
