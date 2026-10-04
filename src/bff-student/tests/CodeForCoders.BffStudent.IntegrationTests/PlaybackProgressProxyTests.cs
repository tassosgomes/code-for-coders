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

public sealed class PlaybackProgressProxyTests
{
    [Fact(DisplayName = nameof(ProgressRequiresCsrfAndUsesMediaAudienceAndReturnsAck))]
    public async Task ProgressRequiresCsrfAndUsesMediaAudienceAndReturnsAck()
    {
        var sessionId = Guid.CreateVersion7();
        var boundary = new Boundary
        {
            Status = 200,
            Body = JsonSerializer.SerializeToUtf8Bytes(new { recorded = true }),
            ContentType = "application/json"
        };
        var identity = Identity(200);
        await using var app = await Server(boundary, identity);
        using var client = Client(app);

        var path = $"/api/v1/playback-sessions/{sessionId:D}/progress";
        var progressPayload = new StringContent(
            JsonSerializer.Serialize(new { sequence = 1, positionSeconds = 30, reason = "heartbeat" }),
            System.Text.Encoding.UTF8,
            "application/json");

        // Without CSRF token and origin: 403 Forbidden
        using var forbidden = await client.PostAsync(path, progressPayload, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Null(boundary.Token);

        // With CSRF token and origin: 200 OK
        client.DefaultRequestHeaders.Add("Origin", "http://student.test");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf");

        using var retryPayload = new StringContent(
            JsonSerializer.Serialize(new { sequence = 1, positionSeconds = 30, reason = "heartbeat" }),
            System.Text.Encoding.UTF8,
            "application/json");
        using var response = await client.PostAsync(path, retryPayload, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("media-student-jwt", boundary.Token);
        Assert.Equal($"/internal/v1/playback-sessions/{sessionId:D}/progress", boundary.Path);
        Assert.Equal(HttpMethod.Post, boundary.Method);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(json.RootElement.GetProperty("recorded").GetBoolean());
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "media", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = nameof(ProgressForwardsRequestBodyAndReturnsRecordedFalseWhenNotAccepted))]
    public async Task ProgressForwardsRequestBodyAndReturnsRecordedFalseWhenNotAccepted()
    {
        var sessionId = Guid.CreateVersion7();
        var boundary = new Boundary
        {
            Status = 200,
            Body = JsonSerializer.SerializeToUtf8Bytes(new { recorded = false }),
            ContentType = "application/json"
        };
        var identity = Identity(200);
        await using var app = await Server(boundary, identity);
        using var client = Client(app);
        client.DefaultRequestHeaders.Add("Origin", "http://student.test");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf");

        var path = $"/api/v1/playback-sessions/{sessionId:D}/progress";
        var payloadObj = new { sequence = 2, positionSeconds = 35, reason = "heartbeat" };
        using var content = new StringContent(JsonSerializer.Serialize(payloadObj), System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(boundary.ReceivedBody);
        using var receivedJson = JsonDocument.Parse(boundary.ReceivedBody);
        Assert.Equal(2, receivedJson.RootElement.GetProperty("sequence").GetInt32());
        Assert.Equal(35, receivedJson.RootElement.GetProperty("positionSeconds").GetInt32());
        Assert.Equal("heartbeat", receivedJson.RootElement.GetProperty("reason").GetString());

        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(responseJson.RootElement.GetProperty("recorded").GetBoolean());
    }

    [Theory(DisplayName = nameof(ProgressPreservesContractualErrors))]
    [InlineData(400, "VALIDATION_ERROR")]
    [InlineData(404, "PLAYBACK_SESSION_NOT_FOUND")]
    [InlineData(410, "PLAYBACK_SESSION_EXPIRED")]
    public async Task ProgressPreservesContractualErrors(int status, string code)
    {
        var sessionId = Guid.CreateVersion7();
        var boundary = new Boundary
        {
            Status = status,
            Body = JsonSerializer.SerializeToUtf8Bytes(new { code }),
            ContentType = "application/json"
        };
        await using var app = await Server(boundary, Identity(200));
        using var client = Client(app);
        client.DefaultRequestHeaders.Add("Origin", "http://student.test");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf");

        var path = $"/api/v1/playback-sessions/{sessionId:D}/progress";
        using var content = new StringContent(
            JsonSerializer.Serialize(new { sequence = 1, positionSeconds = 30, reason = "heartbeat" }),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

        Assert.Equal(status, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(ProgressEnforces3SecondTimeoutReturningUpstreamTimeout504))]
    public async Task ProgressEnforces3SecondTimeoutReturningUpstreamTimeout504()
    {
        var sessionId = Guid.CreateVersion7();
        var boundary = new Boundary { Timeout = true };
        await using var app = await Server(boundary, Identity(200));
        using var client = Client(app);
        client.DefaultRequestHeaders.Add("Origin", "http://student.test");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf");

        var path = $"/api/v1/playback-sessions/{sessionId:D}/progress";
        using var content = new StringContent(
            JsonSerializer.Serialize(new { sequence = 1, positionSeconds = 30, reason = "heartbeat" }),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("UPSTREAM_TIMEOUT", json.RootElement.GetProperty("code").GetString());
    }

    private static Mock<IStudentSessionIdentityClient> Identity(int status)
    {
        var mock = new Mock<IStudentSessionIdentityClient>();
        mock.Setup(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StudentSessionValidatedResult(status, null, Guid.CreateVersion7(), "Student", DateTimeOffset.UtcNow.AddMinutes(5), "media-student-jwt"));
        return mock;
    }

    private static HttpClient Client(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", "student_session=opaque");
        return client;
    }

    private static async Task<WebApplication> Server(Boundary boundary, Mock<IStudentSessionIdentityClient> identity)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Media:BaseAddress"] = "http://media.test/" });
        builder.Services.AddMediaClientConfiguration(builder.Configuration);
        builder.Services.AddHttpClient<IPlaybackMediaClient, PlaybackMediaClient>().ConfigurePrimaryHttpMessageHandler(() => boundary);
        builder.Services.AddOptions<BffSecurityOptions>();
        builder.Services.Configure<StudentSpaCorsOptions>(options => options.AllowedOrigins = ["http://student.test"]);
        builder.Services.AddSingleton(TimeProvider.System);
        var session = new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Student", "csrf", DateTimeOffset.UtcNow.AddMinutes(5));
        var store = new Mock<IBffSessionStore>();
        store.Setup(s => s.GetAsync("opaque", It.IsAny<CancellationToken>())).ReturnsAsync(session);
        store.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<OpaqueBffSession>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(store.Object);
        builder.Services.AddSingleton(identity.Object);
        var app = builder.Build();
        app.UseMiddleware<BffSecurityMiddleware>();
        app.MapPlaybackSessionEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class Boundary : HttpMessageHandler
    {
        public byte[] Body { get; set; } = [];
        public int Status { get; set; } = 200;
        public string ContentType { get; set; } = "application/json";
        public string? Token { get; private set; }
        public string? Path { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? ReceivedBody { get; private set; }
        public bool Timeout { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Token = request.Headers.Authorization?.Parameter;
            Path = request.RequestUri!.AbsolutePath;
            Method = request.Method;
            if (request.Content != null)
            {
                ReceivedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            if (Timeout) throw new OperationCanceledException();
            var content = new ByteArrayContent(Body);
            content.Headers.ContentType = new(ContentType);
            return new HttpResponseMessage((HttpStatusCode)Status) { Content = content };
        }
    }
}
