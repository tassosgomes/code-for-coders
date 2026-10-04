using System.Net;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Endpoints;
using CodeForCoders.BffStudent.Api.Extensions;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CodeForCoders.BffStudent.IntegrationTests;

public sealed class PlaybackProxyTests
{
    [Theory(DisplayName = nameof(DeliveryPreservesBytesAndContentTypeAndRequestsMediaAudience))]
    [InlineData("playlist", "application/vnd.apple.mpegurl")]
    [InlineData("variants/480p", "application/vnd.apple.mpegurl")]
    [InlineData("key", "application/octet-stream")]
    public async Task DeliveryPreservesBytesAndContentTypeAndRequestsMediaAudience(string resource, string contentType)
    {
        var boundary = new Boundary { Body = [0, 1, 127, 128, 255], ContentType = contentType };
        var identity = Identity(200); await using var app = await Server(boundary, identity); using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/playback-sessions/{Guid.CreateVersion7():D}/{resource}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(boundary.Body, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(contentType, response.Content.Headers.ContentType!.MediaType); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("media-student-jwt", boundary.Token);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "media", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = nameof(OpeningRequiresCsrfAndUsesMediaAudience))]
    public async Task OpeningRequiresCsrfAndUsesMediaAudience()
    {
        var session = Guid.CreateVersion7(); var boundary = new Boundary { Status = 201, Body = JsonSerializer.SerializeToUtf8Bytes(new { sessionId = session }), ContentType = "application/json" };
        var identity = Identity(200); await using var app = await Server(boundary, identity); using var client = Client(app);
        var path = $"/api/v1/lessons/{Guid.CreateVersion7():D}/playback-sessions";
        using var forbidden = await client.PostAsync(path, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); Assert.Null(boundary.Token);
        client.DefaultRequestHeaders.Add("Origin", "http://student.test"); client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf");
        using var response = await client.PostAsync(path, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.Equal("/api/v1/playback-sessions/" + session, response.Headers.Location!.OriginalString);
        Assert.Equal(boundary.Body, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "media", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = nameof(RenewalRequiresCsrfAndUsesMediaAudience))]
    public async Task RenewalRequiresCsrfAndUsesMediaAudience()
    {
        var boundary = new Boundary { Body = JsonSerializer.SerializeToUtf8Bytes(new { sessionId = Guid.CreateVersion7() }), ContentType = "application/json" };
        var identity = Identity(200); await using var app = await Server(boundary, identity); using var client = Client(app);
        var path = $"/api/v1/playback-sessions/{Guid.CreateVersion7():D}/renewals";
        using var forbidden = await client.PostAsync(path, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); Assert.Null(boundary.Token);
        client.DefaultRequestHeaders.Add("Origin", "http://student.test"); client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf");
        using var response = await client.PostAsync(path, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(boundary.Body, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.EndsWith("/renewals", boundary.Path); Assert.Equal(HttpMethod.Post, boundary.Method);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "media", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = nameof(RenewalPreservesContractualErrorsAndEndedAccess))]
    [InlineData(403, "ACCESS_DENIED")]
    [InlineData(503, "ACCESS_DECISION_UNAVAILABLE")]
    [InlineData(404, "PLAYBACK_SESSION_NOT_FOUND")]
    [InlineData(410, "PLAYBACK_SESSION_EXPIRED")]
    [InlineData(422, "WATERMARK_UNAVAILABLE")]
    public async Task RenewalPreservesContractualErrorsAndEndedAccess(int status, string code)
    {
        var ended = DateTimeOffset.UtcNow;
        var boundary = new Boundary { Status = status, Body = JsonSerializer.SerializeToUtf8Bytes(new { code, reason = "grant-ended", accessEndedAt = ended }) };
        await using var app = await Server(boundary, Identity(200)); using var client = Client(app);
        client.DefaultRequestHeaders.Add("Origin", "http://student.test"); client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf");
        using var response = await client.PostAsync($"/api/v1/playback-sessions/{Guid.CreateVersion7():D}/renewals", null, TestContext.Current.CancellationToken);
        Assert.Equal(status, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal("grant-ended", json.RootElement.GetProperty("reason").GetString());
        Assert.Equal(ended, json.RootElement.GetProperty("accessEndedAt").GetDateTimeOffset());
    }

    [Theory(DisplayName = nameof(MediaErrorsKeepTheirPublicCodes))]
    [InlineData(409, "MEDIA_NOT_READY")]
    [InlineData(422, "WATERMARK_UNAVAILABLE")]
    [InlineData(403, "ACCESS_DENIED")]
    [InlineData(503, "ACCESS_DECISION_UNAVAILABLE")]
    [InlineData(404, "PLAYBACK_SESSION_NOT_FOUND")]
    [InlineData(410, "PLAYBACK_SESSION_EXPIRED")]
    public async Task MediaErrorsKeepTheirPublicCodes(int status, string code)
    {
        var boundary = new Boundary { Status = status, Body = JsonSerializer.SerializeToUtf8Bytes(new { code }) };
        await using var app = await Server(boundary, Identity(200)); using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/playback-sessions/{Guid.CreateVersion7():D}/key", TestContext.Current.CancellationToken);
        Assert.Equal(status, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(TimeoutMapsTo504))]
    public async Task TimeoutMapsTo504()
    {
        await using var app = await Server(new Boundary { Timeout = true }, Identity(200)); using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/playback-sessions/{Guid.CreateVersion7():D}/playlist", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact(DisplayName = nameof(MissingSessionNeverCallsIdentityOrMedia))]
    public async Task MissingSessionNeverCallsIdentityOrMedia()
    {
        var identity = Identity(200); var boundary = new Boundary();
        await using var app = await Server(boundary, identity); using var client = app.GetTestClient();
        using var response = await client.GetAsync($"/api/v1/playback-sessions/{Guid.CreateVersion7():D}/key", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Null(boundary.Token);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IStudentSessionIdentityClient> Identity(int status)
    {
        var mock = new Mock<IStudentSessionIdentityClient>();
        mock.Setup(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StudentSessionValidatedResult(status, null, Guid.CreateVersion7(), "Student", DateTimeOffset.UtcNow.AddMinutes(5), "media-student-jwt"));
        return mock;
    }
    private static HttpClient Client(WebApplication app) { var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("Cookie", "student_session=opaque"); return client; }

    private static async Task<WebApplication> Server(Boundary boundary, Mock<IStudentSessionIdentityClient> identity)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Media:BaseAddress"] = "http://media.test/" });
        builder.Services.AddMediaClientConfiguration(builder.Configuration);
        builder.Services.AddHttpClient<IPlaybackMediaClient, PlaybackMediaClient>().ConfigurePrimaryHttpMessageHandler(() => boundary);
        builder.Services.AddOptions<BffSecurityOptions>(); builder.Services.Configure<StudentSpaCorsOptions>(options => options.AllowedOrigins = ["http://student.test"]);
        builder.Services.AddSingleton(TimeProvider.System);
        var session = new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Student", "csrf", DateTimeOffset.UtcNow.AddMinutes(5));
        var store = new Mock<IBffSessionStore>(); store.Setup(s => s.GetAsync("opaque", It.IsAny<CancellationToken>())).ReturnsAsync(session);
        store.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<OpaqueBffSession>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(store.Object); builder.Services.AddSingleton(identity.Object);
        var app = builder.Build(); app.UseMiddleware<BffSecurityMiddleware>(); app.MapPlaybackSessionEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken); return app;
    }

    private sealed class Boundary : HttpMessageHandler
    {
        public byte[] Body { get; set; } = [];
        public int Status { get; set; } = 200;
        public string ContentType { get; set; } = "application/octet-stream";
        public string? Token { get; private set; }
        public string? Path { get; private set; }
        public HttpMethod? Method { get; private set; }
        public bool Timeout { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Token = request.Headers.Authorization?.Parameter;
            Path = request.RequestUri!.AbsolutePath; Method = request.Method;
            if (Timeout) throw new OperationCanceledException();
            var content = new ByteArrayContent(Body); content.Headers.ContentType = new(ContentType);
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Status) { Content = content });
        }
    }
}
