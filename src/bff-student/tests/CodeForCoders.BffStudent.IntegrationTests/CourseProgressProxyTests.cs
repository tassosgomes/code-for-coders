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

public sealed class CourseProgressProxyTests
{
    private static readonly Guid CourseId = Guid.CreateVersion7();
    [Fact(DisplayName = nameof(GetRequestsLearningAudienceAndForwardsProgressWithoutCsrf))]
    public async Task GetRequestsLearningAudienceAndForwardsProgressWithoutCsrf()
    {
        var boundary = Boundary(); var identity = Identity(200);
        await using var app = await Server(boundary, identity); using var client = Client(app);
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/internal/v1/student-courses/{CourseId}/progress", boundary.Path);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(37, body.RootElement.GetProperty("percent").GetInt32()); Assert.Equal("learning-student-jwt", boundary.Token);
        Assert.DoesNotContain("learning-student-jwt", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.CacheControl!.NoStore);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "learning", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory(DisplayName = nameof(LearningErrorsKeepPublicCodeAndDeniedReason))]
    [InlineData(403, "ACCESS_DENIED")]
    [InlineData(404, "COURSE_NOT_AVAILABLE")]
    [InlineData(503, "ACCESS_DECISION_UNAVAILABLE")]
    [InlineData(500, "UPSTREAM_UNAVAILABLE")]
    public async Task LearningErrorsKeepPublicCodeAndDeniedReason(int status, string code)
    {
        var boundary = new StudentLessonBoundaryHandler { StatusCode = status, Body = JsonSerializer.Serialize(new { code, reason = "grant-ended", accessEndedAt = "2026-10-02T03:00:00Z" }) };
        await using var app = await Server(boundary, Identity(200)); using var client = Client(app);
        using var response = await client.GetAsync(Path(), TestContext.Current.CancellationToken);
        Assert.Equal(status == 500 ? 502 : status, (int)response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        if (status == 403) { Assert.Equal("grant-ended", body.RootElement.GetProperty("reason").GetString()); Assert.True(body.RootElement.TryGetProperty("accessEndedAt", out _)); }
        Assert.False(body.RootElement.TryGetProperty("courseId", out _));
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
    [InlineData("{\"courseId\":null,\"percent\":null}")]
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
            courseId = CourseId,
            versionNumber = 1,
            completedLessons = 3,
            totalLessons = 8,
            percent = 37,
            lessons = new[] { new { lessonId = Guid.CreateVersion7(), completed = true, lastPositionSeconds = 252, resumeAtSeconds = 252 } }
        })
    };
    private static string Path() => $"/api/v1/courses/{CourseId}/progress";
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
        builder.Services.AddHttpClient<ICourseProgressLearningClient, CourseProgressLearningClient>().ConfigurePrimaryHttpMessageHandler(() => boundary);
        builder.Services.AddOptions<BffSecurityOptions>(); builder.Services.AddOptions<StudentSpaCorsOptions>(); builder.Services.AddSingleton(TimeProvider.System);
        var session = new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Student", "csrf", DateTimeOffset.UtcNow.AddMinutes(5));
        var store = new Mock<IBffSessionStore>(); store.Setup(s => s.GetAsync("opaque", It.IsAny<CancellationToken>())).ReturnsAsync(session);
        store.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<OpaqueBffSession>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(store.Object); builder.Services.AddSingleton(identity.Object);
        var app = builder.Build(); app.UseMiddleware<BffSecurityMiddleware>(); app.MapCourseProgressEndpoints(); await app.StartAsync(TestContext.Current.CancellationToken); return app;
    }
}
