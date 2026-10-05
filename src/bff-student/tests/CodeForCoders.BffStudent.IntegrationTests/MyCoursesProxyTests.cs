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

public sealed class MyCoursesProxyTests
{
    private static readonly Guid CourseId = Guid.CreateVersion7();
    [Fact(DisplayName = nameof(GetRequestsLearningAudienceAndForwardsCoursesWithoutCsrf))]
    public async Task GetRequestsLearningAudienceAndForwardsCoursesWithoutCsrf()
    {
        var boundary = Boundary(); var identity = Identity(200);
        await using var app = await Server(boundary, identity); using var client = Client(app);
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/internal/v1/student-courses", boundary.Path);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(37, body.RootElement.GetProperty("active")[0].GetProperty("progress").GetProperty("percent").GetInt32()); Assert.Equal("learning-student-jwt", boundary.Token);
        Assert.DoesNotContain("learning-student-jwt", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.CacheControl!.NoStore);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "learning", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = nameof(LearningErrorsKeepPublicCodeOnlyForAccessUnavailability))]
    [InlineData(503, "COURSE_ACCESS_UNAVAILABLE", 503, "COURSE_ACCESS_UNAVAILABLE")]
    [InlineData(503, "OTHER_FAILURE", 502, "UPSTREAM_UNAVAILABLE")]
    [InlineData(500, "INTERNAL_ERROR", 502, "UPSTREAM_UNAVAILABLE")]
    [InlineData(403, "TOKEN_INVALID", 502, "UPSTREAM_UNAVAILABLE")]
    public async Task LearningErrorsKeepPublicCodeOnlyForAccessUnavailability(int status, string code, int expectedStatus, string expectedCode)
    {
        var boundary = new StudentLessonBoundaryHandler { StatusCode = status, Body = JsonSerializer.Serialize(new { code }) };
        await using var app = await Server(boundary, Identity(200)); using var client = Client(app);
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectedCode, body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("active", out _));
    }

    [Fact(DisplayName = nameof(LearningTimeoutMapsTo504))]
    public async Task LearningTimeoutMapsTo504()
    {
        await using var app = await Server(new StudentLessonBoundaryHandler { Timeout = true }, Identity(200)); using var client = Client(app);
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Theory(DisplayName = nameof(IdentityFailuresMapUpstreamAndNeverCallLearning))]
    [InlineData(502)]
    [InlineData(504)]
    [InlineData(503)]
    public async Task IdentityFailuresMapUpstreamAndNeverCallLearning(int status)
    {
        var boundary = Boundary(); await using var app = await Server(boundary, Identity(status)); using var client = Client(app);
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(status == 503 ? 502 : status, (int)response.StatusCode); Assert.Null(boundary.Token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(status == 504 ? "UPSTREAM_TIMEOUT" : "UPSTREAM_UNAVAILABLE", json.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(MissingSessionNeverCallsIdentityOrLearning))]
    public async Task MissingSessionNeverCallsIdentityOrLearning()
    {
        var identity = Identity(200); var boundary = Boundary();
        await using var app = await Server(boundary, identity); using var client = app.GetTestClient();
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Null(boundary.Token);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = nameof(MalformedLearningBodiesMapTo502))]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"code\":5}")]
    [InlineData("{\"progressAvailable\":true,\"active\":[{}],\"ended\":[]}")]
    public async Task MalformedLearningBodiesMapTo502(string body)
    {
        await using var app = await Server(new StudentLessonBoundaryHandler { Body = body }, Identity(200));
        using var client = Client(app);
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("UPSTREAM_UNAVAILABLE", result.RootElement.GetProperty("code").GetString());
    }

    private static StudentLessonBoundaryHandler Boundary() => new()
    {
        Body = JsonSerializer.Serialize(new
        {
            progressAvailable = true,
            active = new[] { new { courseId = CourseId, title = "Current course", started = true,
                lastActivityAt = "2026-10-04T12:00:00Z", continueLessonId = Guid.CreateVersion7(),
                progress = new { completedLessons = 3, totalLessons = 8, percent = 37 } } },
            ended = Array.Empty<object>()
        })
    };
    private static string Path() => "/api/v1/my-courses";
    private static Mock<IStudentSessionIdentityClient> Identity(int status)
    {
        var mock = new Mock<IStudentSessionIdentityClient>();
        mock.Setup(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StudentSessionValidatedResult(status, null, Guid.CreateVersion7(), "Student", DateTimeOffset.UtcNow.AddMinutes(5), "learning-student-jwt"));
        return mock;
    }
    private static HttpClient Client(WebApplication app) { var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("Cookie", "student_session=opaque"); return client; }
    private static async Task<WebApplication> Server(StudentLessonBoundaryHandler boundary, Mock<IStudentSessionIdentityClient> identity)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Learning:BaseAddress"] = "http://learning.test/" });
        builder.Services.AddLearningClientConfiguration(builder.Configuration);
        builder.Services.AddHttpClient<IMyCoursesLearningClient, MyCoursesLearningClient>().ConfigurePrimaryHttpMessageHandler(() => boundary);
        builder.Services.AddOptions<BffSecurityOptions>(); builder.Services.AddOptions<StudentSpaCorsOptions>(); builder.Services.AddSingleton(TimeProvider.System);
        var session = new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Student", "csrf", DateTimeOffset.UtcNow.AddMinutes(5));
        var store = new Mock<IBffSessionStore>(); store.Setup(s => s.GetAsync("opaque", It.IsAny<CancellationToken>())).ReturnsAsync(session);
        store.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<OpaqueBffSession>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(store.Object); builder.Services.AddSingleton(identity.Object);
        var app = builder.Build(); app.UseMiddleware<BffSecurityMiddleware>(); app.MapMyCoursesEndpoints(); await app.StartAsync(TestContext.Current.CancellationToken); return app;
    }
}
