using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseAuthoringAccessTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealHostAndHttpClientCreateListAndReadWithServerCredentials))]
    public async Task RealHostAndHttpClientCreateListAndReadWithServerCredentials()
    {
        await using var factory = new CourseBffApiFactory();
        using var client = await factory.AuthenticatedAsync();
        Assert.IsType<CourseAuthoringClient>(factory.Services.GetRequiredService<ICourseAuthoringClient>());
        client.DefaultRequestHeaders.Authorization = new("Bearer", "browser-forgery");
        client.DefaultRequestHeaders.Add("X-Actor-Name", "Forged name");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/courses") { Content = JsonContent.Create(new { title = "New course", description = "Learn APIs" }) };
        request.Headers.Add("Idempotency-Key", "course-intent");
        using var created = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/v1/courses/{factory.Learning.CourseId:D}", created.Headers.Location?.OriginalString);
        Assert.Equal("learning", factory.Identity.LastAudience);
        Assert.Equal("server-learning-token", factory.Learning.Token);
        Assert.Equal("Validated teacher", factory.Learning.ActorName);
        Assert.Equal("course-intent", factory.Learning.Key);
        Assert.Equal("New course", factory.Learning.Body?.Title);
        using var list = await client.GetAsync("/api/v1/courses?_page=1&_size=20&status=draft", Cancellation);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("status=draft", factory.Learning.Uri!.Query);
        using var detail = await client.GetAsync(created.Headers.Location, Cancellation);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact(DisplayName = nameof(ReaderCanReadButWritingAndMissingCsrfNeverReachLearning))]
    public async Task ReaderCanReadButWritingAndMissingCsrfNeverReachLearning()
    {
        await using var factory = new CourseBffApiFactory();
        factory.Identity.Permissions = ["autoria.ler"];
        using var client = await factory.AuthenticatedAsync();
        using var read = await client.GetAsync("/api/v1/courses", Cancellation);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var calls = factory.Learning.Calls;
        using var write = await client.PostAsJsonAsync("/api/v1/courses", new { title = "Denied" }, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(calls, factory.Learning.Calls);
        factory.Identity.Permissions = ["autoria.ler", "autoria.editar"];
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        using var csrf = await client.PostAsJsonAsync("/api/v1/courses", new { title = "Denied" }, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        Assert.Equal(calls, factory.Learning.Calls);
    }

    [Fact(DisplayName = nameof(RevokedSessionReturns401OnTheNextAction))]
    public async Task RevokedSessionReturns401OnTheNextAction()
    {
        await using var factory = new CourseBffApiFactory();
        using var client = await factory.AuthenticatedAsync();
        using var first = await client.GetAsync("/api/v1/courses", Cancellation);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        factory.Identity.Revoked = true;
        using var next = await client.GetAsync("/api/v1/courses", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, next.StatusCode);
        Assert.Equal(1, factory.Learning.Calls);
        Assert.Null(await factory.Sessions.GetAsync("opaque-course-session", Cancellation));
    }

    [Fact(DisplayName = nameof(RealAdapterPreserves404AndHandlesMalformedUpstream))]
    public async Task RealAdapterPreserves404AndHandlesMalformedUpstream()
    {
        await using var factory = new CourseBffApiFactory();
        using var client = await factory.AuthenticatedAsync();
        factory.Learning.Status = HttpStatusCode.NotFound;
        using var missing = await client.GetAsync($"/api/v1/courses/{Guid.CreateVersion7()}", Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("COURSE_NOT_FOUND", await missing.Content.ReadAsStringAsync(Cancellation));
        factory.Learning.Status = HttpStatusCode.OK; factory.Learning.Malformed = true;
        using var malformed = await client.GetAsync("/api/v1/courses", Cancellation);
        Assert.Equal(HttpStatusCode.BadGateway, malformed.StatusCode);
    }

    [Fact(DisplayName = nameof(AdministratorWithoutAuthoringCannotUseDirectLinks))]
    public async Task AdministratorWithoutAuthoringCannotUseDirectLinks()
    {
        await using var factory = new CourseBffApiFactory();
        factory.Identity.Permissions = ["acesso.gerir"];
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync("/api/v1/courses", Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, factory.Learning.Calls);
    }
}
