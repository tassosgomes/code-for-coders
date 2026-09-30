using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseVersionProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealClientAndDiReturnOrderedVersionHistoryWithMetadata))]
    public async Task RealClientAndDiReturnOrderedVersionHistoryWithMetadata()
    {
        using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}/versions?_page=1&_size=20", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("learning", factory.Identity.LastAudience);
        Assert.Equal("server-learning-token", factory.Learning.Token); Assert.Equal("?_page=1&_size=20", factory.Learning.Uri!.Query);
        var history = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(2, history.GetProperty("data")[0].GetProperty("versionNumber").GetInt32());
        Assert.True(history.GetProperty("data")[0].GetProperty("current").GetBoolean());
        Assert.Equal("Second note", history.GetProperty("data")[0].GetProperty("versionNote").GetString());
        Assert.Equal("Validated teacher", history.GetProperty("data")[0].GetProperty("publishedBy").GetProperty("name").GetString());
    }

    [Fact(DisplayName = nameof(HistoricalReadUsesTheRequestedVersionWithoutMediaEnrichment))]
    public async Task HistoricalReadUsesTheRequestedVersionWithoutMediaEnrichment()
    {
        using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}/versions/1", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("Historical title", version.GetProperty("title").GetString()); Assert.False(version.GetProperty("current").GetBoolean());
        Assert.EndsWith("/versions/1", factory.Learning.Uri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(DiscardForwardsDisplayedRevisionActorAndIntentThroughRealClient))]
    public async Task DiscardForwardsDisplayedRevisionActorAndIntentThroughRealClient()
    {
        using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        using var response = await DiscardAsync(client, factory.Learning.CourseId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(HttpMethod.Post, factory.Learning.Method);
        Assert.Equal(4, factory.Learning.Payload!.Value.GetProperty("draftRevision").GetInt32());
        Assert.Equal("discard-intent", factory.Learning.Key); Assert.Equal("Validated teacher", factory.Learning.ActorName);
        var course = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(5, course.GetProperty("draftRevision").GetInt32()); Assert.False(course.GetProperty("hasUnpublishedChanges").GetBoolean());
    }

    [Fact(DisplayName = nameof(DiscardConflictsAndVersionNotFoundArePreservedAndUpstreamErrorsSanitized))]
    public async Task DiscardConflictsAndVersionNotFoundArePreservedAndUpstreamErrorsSanitized()
    {
        using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        foreach (var code in new[] { "DRAFT_CHANGED", "COURSE_NEVER_PUBLISHED" })
        {
            factory.Learning.Status = HttpStatusCode.Conflict; factory.Learning.ProblemCode = code;
            using var response = await DiscardAsync(client, factory.Learning.CourseId); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        }
        factory.Learning.Status = HttpStatusCode.NotFound; factory.Learning.ProblemCode = "VERSION_NOT_FOUND";
        using var missing = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}/versions/9", Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); Assert.Equal("VERSION_NOT_FOUND", (await missing.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        factory.Learning.Status = HttpStatusCode.GatewayTimeout;
        using var failed = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}/versions", Cancellation); Assert.Equal(HttpStatusCode.GatewayTimeout, failed.StatusCode);
        Assert.Equal("LEARNING_UNAVAILABLE", (await failed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(ReaderCanReadHistoryButCannotDiscardAndInvalidPaginationNeverReachesUpstream))]
    public async Task ReaderCanReadHistoryButCannotDiscardAndInvalidPaginationNeverReachesUpstream()
    {
        using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["autoria.ler"]; using var client = await factory.AuthenticatedAsync();
        using var history = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}/versions", Cancellation); Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var calls = factory.Learning.Calls;
        using var discard = await DiscardAsync(client, factory.Learning.CourseId); Assert.Equal(HttpStatusCode.Forbidden, discard.StatusCode); Assert.Equal(calls, factory.Learning.Calls);
        using var invalid = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}/versions?_size=100", Cancellation); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.Equal(calls, factory.Learning.Calls);
    }

    private static async Task<HttpResponseMessage> DiscardAsync(HttpClient client, Guid courseId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{courseId}/discard-draft") { Content = JsonContent.Create(new { draftRevision = 4 }) };
        request.Headers.Add("Idempotency-Key", "discard-intent"); return await client.SendAsync(request, Cancellation);
    }
}
