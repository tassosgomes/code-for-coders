using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class VideoTranscriptionReadTests(BffAdminApiFactory factory)
{
    private const int MaximumResponseBytes = 8 * 1024 * 1024;
    private const string PrivateText = "Private classroom marker";

    [Fact]
    public async Task TeacherReadsExactBodyAndHeadersThroughRealClientWithoutTextInLogsOrUrl()
    {
        var (client, cookie) = await TeacherAsync();
        var id = Guid.CreateVersion7();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            videoId = id,
            revision = "tr_7d2a91c4",
            language = "pt-BR",
            durationMs = 10_000,
            readyAt = DateTimeOffset.UtcNow,
            segments = new[] { new { startMs = 0, endMs = 4100, text = PrivateText + " <b>literal</b>" } },
        }));
        factory.VideoLibraryHandler.TranscriptionContentFactory = () => JsonBody(body);
        var logsBefore = factory.CapturedLogs.Entries.Count;
        using (client) using (var request = Request(id, cookie))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(body, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
            Assert.True(response.Headers.CacheControl?.Private);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal(HttpMethod.Get, factory.VideoLibraryHandler.LastMethod);
            Assert.Equal($"/internal/v1/videos/{id:D}/transcription", factory.VideoLibraryHandler.LastPath);
            Assert.Equal("teacher-media-token", factory.VideoLibraryHandler.LastAccessToken);
            Assert.Equal("media", factory.StaffSessionIdentityHandler.LastValidationAudience);
            Assert.Null(factory.VideoLibraryHandler.LastBody);
            Assert.Null(factory.VideoLibraryHandler.LastIdempotencyKey);
            var logs = factory.CapturedLogs.Entries.Skip(logsBefore).ToArray();
            Assert.NotEmpty(logs);
            Assert.DoesNotContain(logs, log => log.Contains(PrivateText, StringComparison.Ordinal));
            Assert.DoesNotContain(PrivateText, factory.VideoLibraryHandler.LastPath!);
            Assert.Equal(string.Empty, request.RequestUri!.Query);
        }
    }

    [Fact]
    public async Task MissingPermissionIsRejectedBeforeCallingMedia()
    {
        var (client, cookie) = await TeacherAsync();
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession! with { Permissions = [] };
        using (client) using (var request = Request(Guid.CreateVersion7(), cookie))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
            Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
        }
    }

    [Theory]
    [InlineData(409, "TRANSCRIPTION_NOT_READY")]
    [InlineData(404, "VIDEO_NOT_FOUND")]
    [InlineData(403, "PERMISSION_DENIED")]
    [InlineData(401, "TOKEN_INVALID")]
    [InlineData(503, "MEDIA_UNAVAILABLE")]
    [InlineData(504, "MEDIA_UNAVAILABLE")]
    public async Task MediaProblemStatusAndCodeAreMapped(int status, string code)
    {
        var (client, cookie) = await TeacherAsync();
        factory.VideoLibraryHandler.StatusCode = (HttpStatusCode)status;
        factory.VideoLibraryHandler.ProblemCode = code;
        using (client) using (var request = Request(Guid.CreateVersion7(), cookie))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            await AssertProblemAsync(response, status == 503 ? HttpStatusCode.BadGateway : (HttpStatusCode)status, code);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResponseOverEightMiBIsRejectedForKnownAndUnknownLength(bool knownLength)
    {
        var (client, cookie) = await TeacherAsync();
        var bytes = new byte[MaximumResponseBytes + 1];
        factory.VideoLibraryHandler.TranscriptionContentFactory = () => knownLength ? JsonBody(bytes) : new UnknownLengthContent(bytes);
        using (client) using (var request = Request(Guid.CreateVersion7(), cookie))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            await AssertProblemAsync(response, HttpStatusCode.BadGateway, "MEDIA_UNAVAILABLE");
            Assert.True((await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length < 1024);
        }
    }

    [Fact]
    public async Task ResponseAtEightMiBBoundaryIsForwardedWithoutTruncation()
    {
        var (client, cookie) = await TeacherAsync();
        var bytes = Encoding.UTF8.GetBytes("{\"text\":\"" + new string('a', MaximumResponseBytes - 11) + "\"}");
        Assert.Equal(MaximumResponseBytes, bytes.Length);
        factory.VideoLibraryHandler.TranscriptionContentFactory = () => new UnknownLengthContent(bytes);
        using (client) using (var request = Request(Guid.CreateVersion7(), cookie))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }
    }

    private async Task<(HttpClient Client, string Cookie)> TeacherAsync()
    {
        factory.SessionStore.Reset(); factory.StaffSessionIdentityHandler.Reset(); factory.VideoLibraryHandler.Reset();
        var client = factory.CreateClient();
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        loginRequest.Headers.Add("Idempotency-Key", "transcription-read-login");
        using var loginResponse = await client.SendAsync(loginRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = await loginResponse.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        factory.StaffSessionIdentityHandler.ValidatedSession = new StaffSessionValidatedV1(
            session.AccountId, "Marina Alves", ["professor"], ["midia.enviar"], DateTimeOffset.UtcNow.AddMinutes(30), "teacher-media-token");
        return (client, loginResponse.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0]);
    }

    private static HttpRequestMessage Request(Guid videoId, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/videos/{videoId:D}/transcription");
        request.Headers.Add("Cookie", cookie); return request;
    }
    private static ByteArrayContent JsonBody(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new("application/json") { CharSet = "utf-8" }; return content;
    }
    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] body;
        public UnknownLengthContent(byte[] body)
        {
            this.body = body; Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(body).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(body, writable: false));
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => CreateContentReadStreamAsync();
    }
}
