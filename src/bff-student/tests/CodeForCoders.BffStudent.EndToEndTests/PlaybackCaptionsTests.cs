using System.Net;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Application.Common;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffStudent.EndToEndTests;

[Collection(BffStudentApiCollection.Name)]
public sealed class PlaybackCaptionsTests(BffStudentApiFactory factory)
{
    private readonly Guid session = Guid.CreateVersion7();
    private string Path => $"/api/v1/playback-sessions/{session:D}/captions";
    private const int MaximumBytes = 8 * 1024 * 1024;

    [Fact(DisplayName = nameof(PreservesBodyHeadersAndRequestsMediaAudience))]
    public async Task PreservesBodyHeadersAndRequestsMediaAudience()
    {
        var boundary = new Boundary { Body = System.Text.Encoding.UTF8.GetBytes("WEBVTT\n\n1\n00:00:00.000 --> 00:00:01.000\nIntrodução &amp; texto\n\n") };
        using var host = Host(boundary); using var client = await ClientAsync(host);
        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(boundary.Body, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("text/vtt; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        Assert.True(response.Headers.CacheControl!.Private); Assert.True(response.Headers.CacheControl.NoStore);
        Assert.Equal("media", factory.StudentSessionClient.ValidatedAudience);
        Assert.Equal("media-student-token", boundary.Token);
        Assert.Equal("/internal/v1/playback-sessions/" + session.ToString("D") + "/captions", boundary.Path);
        Assert.Equal(HttpMethod.Get, boundary.Method);
    }

    [Fact(DisplayName = nameof(VisitorCannotReachMedia))]
    public async Task VisitorCannotReachMedia()
    {
        var boundary = new Boundary(); using var host = Host(boundary); using var client = host.CreateClient();
        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Null(boundary.Token);
    }

    [Theory(DisplayName = nameof(PreservesAuthorizationErrorsWithoutTranscriptText))]
    [InlineData(404, "PLAYBACK_SESSION_NOT_FOUND")]
    [InlineData(404, "CAPTIONS_NOT_AVAILABLE")]
    [InlineData(410, "PLAYBACK_SESSION_EXPIRED")]
    public async Task PreservesAuthorizationErrorsWithoutTranscriptText(int status, string code)
    {
        var boundary = new Boundary
        {
            Status = status,
            Body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                code,
                reason = "grant-ended",
                accessEndedAt = "2026-10-04T03:00:00Z",
                segments = new[] { "Must never leak" }
            })
        };
        using var host = Host(boundary); using var client = await ClientAsync(host);
        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(status, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body); Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("Must never leak", body);
        Assert.Equal("grant-ended", json.RootElement.GetProperty("reason").GetString());
    }

    [Theory(DisplayName = nameof(RejectsBodiesOverEightMiBWithOrWithoutContentLength))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsBodiesOverEightMiBWithOrWithoutContentLength(bool unknownLength)
    {
        var boundary = new Boundary { Body = new byte[MaximumBytes + 1], UnknownLength = unknownLength };
        using var host = Host(boundary); using var client = await ClientAsync(host);
        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("UPSTREAM_UNAVAILABLE", json.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(AcceptsExactlyEightMiB))]
    public async Task AcceptsExactlyEightMiB()
    {
        var boundary = new Boundary { Body = new byte[MaximumBytes] };
        using var host = Host(boundary); using var client = await ClientAsync(host);
        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MaximumBytes, (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host(Boundary boundary)
        => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient<IPlaybackMediaClient, PlaybackMediaClient>().ConfigurePrimaryHttpMessageHandler(() => boundary)));

    private async Task<HttpClient> ClientAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        factory.StudentSessionClient.ValidateResult = new(200, null, Guid.CreateVersion7(), "Student", DateTimeOffset.MaxValue, "media-student-token");
        var opaque = Guid.CreateVersion7().ToString("N");
        await factory.SessionStore.StoreAsync(opaque, new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Student", "csrf", DateTimeOffset.MaxValue), TestContext.Current.CancellationToken);
        var client = host.CreateClient(); client.DefaultRequestHeaders.Add("Cookie", "student_session=" + opaque); return client;
    }

    private sealed class Boundary : HttpMessageHandler
    {
        public byte[] Body { get; init; } = [];
        public int Status { get; init; } = 200;
        public bool UnknownLength { get; init; }
        public string? Token { get; private set; }
        public string? Path { get; private set; }
        public HttpMethod? Method { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Token = request.Headers.Authorization?.Parameter; Path = request.RequestUri!.AbsolutePath; Method = request.Method;
            HttpContent content = UnknownLength ? new UnknownLengthContent(Body) : new ByteArrayContent(Body);
            content.Headers.ContentType = new("text/vtt") { CharSet = "utf-8" };
            var response = new HttpResponseMessage((HttpStatusCode)Status) { Content = content };
            response.Headers.TryAddWithoutValidation("Cache-Control", "private, no-store");
            return Task.FromResult(response);
        }
    }
    private sealed class UnknownLengthContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(body).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(body, writable: false));
    }
}
