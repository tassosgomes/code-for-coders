using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CoursePublicationProxyTests
{
    [Fact(DisplayName = nameof(RealClientAndHostForwardPublicationRevisionNoteKeyAndActor))]
    public async Task RealClientAndHostForwardPublicationRevisionNoteKeyAndActor()
    {
        using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        using var response = await PublishAsync(client, factory.Learning.CourseId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.Equal("learning", factory.Identity.LastAudience);
        Assert.Equal("server-learning-token", factory.Learning.Token); Assert.Equal("Validated teacher", factory.Learning.ActorName);
        Assert.Equal("publication-intent", factory.Learning.Key); Assert.Equal(4, factory.Learning.Payload!.Value.GetProperty("draftRevision").GetInt32());
        Assert.EndsWith("/versions/1", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("versionNumber").GetInt32());
    }

    [Fact(DisplayName = nameof(PendenciesArePreservedAcrossTheRealHttpClient))]
    public async Task PendenciesArePreservedAcrossTheRealHttpClient()
    {
        using var factory = new CourseBffApiFactory(); factory.Learning.Status = HttpStatusCode.UnprocessableEntity; factory.Learning.ProblemCode = "COURSE_INCOMPLETE";
        factory.Learning.Pendencies = JsonSerializer.SerializeToElement(new[] { new { code = "lesson-without-video", moduleId = Guid.CreateVersion7(), lessonId = Guid.CreateVersion7() } });
        using var client = await factory.AuthenticatedAsync(); using var response = await PublishAsync(client, factory.Learning.CourseId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("COURSE_INCOMPLETE", problem.GetProperty("code").GetString()); Assert.Single(problem.GetProperty("pendencies").EnumerateArray());
    }

    [Fact(DisplayName = nameof(StaleRevisionRemainsConflictAndUpstreamFailureIsSanitized))]
    public async Task StaleRevisionRemainsConflictAndUpstreamFailureIsSanitized()
    {
        using var factory = new CourseBffApiFactory(); factory.Learning.Status = HttpStatusCode.Conflict; factory.Learning.ProblemCode = "DRAFT_CHANGED";
        using var client = await factory.AuthenticatedAsync(); using var response = await PublishAsync(client, factory.Learning.CourseId);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DRAFT_CHANGED", (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString());
        factory.Learning.Status = HttpStatusCode.BadGateway;
        using var failed = await PublishAsync(client, factory.Learning.CourseId); Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal("LEARNING_UNAVAILABLE", (await failed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(ReaderAndRevokedSessionCannotReachPublicationUpstream))]
    public async Task ReaderAndRevokedSessionCannotReachPublicationUpstream()
    {
        using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["autoria.ler"];
        using var client = await factory.AuthenticatedAsync(); using var response = await PublishAsync(client, factory.Learning.CourseId);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(0, factory.Learning.Calls);
        factory.Identity.Revoked = true;
        using var revoked = await PublishAsync(client, factory.Learning.CourseId); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode); Assert.Equal(0, factory.Learning.Calls);
    }

    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{courseId}/versions") { Content = JsonContent.Create(new { draftRevision = 4, versionNote = "Note" }) };
        request.Headers.Add("Idempotency-Key", "publication-intent"); return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
