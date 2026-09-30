using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseDeletionProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealHostAndClientForwardDeleteActorTokenAndIntentAndReturnEmptyNoContent))]
    public async Task RealHostAndClientForwardDeleteActorTokenAndIntentAndReturnEmptyNoContent()
    {
        using var factory = new CourseBffApiFactory(); factory.Learning.Status = HttpStatusCode.NoContent; using var client = await factory.AuthenticatedAsync();
        using var response = await DeleteAsync(client, factory.Learning.CourseId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); Assert.Empty(await response.Content.ReadAsByteArrayAsync(Cancellation));
        Assert.Equal(HttpMethod.Delete, factory.Learning.Method); Assert.Equal($"/internal/v1/courses/{factory.Learning.CourseId:D}", factory.Learning.Uri!.AbsolutePath);
        Assert.Equal("delete-intent", factory.Learning.Key); Assert.Equal("Validated teacher", factory.Learning.ActorName);
        Assert.Equal("server-learning-token", factory.Learning.Token); Assert.Equal("learning", factory.Identity.LastAudience); Assert.Null(factory.Learning.Payload);
        using var retry = await DeleteAsync(client, factory.Learning.CourseId); Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact(DisplayName = nameof(PublishedConflictAndTenantNotFoundArePreservedWhileUpstreamFailuresAreSanitized))]
    public async Task PublishedConflictAndTenantNotFoundArePreservedWhileUpstreamFailuresAreSanitized()
    {
        using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        foreach (var (status, code) in new[] { (HttpStatusCode.Conflict, "COURSE_ALREADY_PUBLISHED"), (HttpStatusCode.NotFound, "COURSE_NOT_FOUND"), (HttpStatusCode.ServiceUnavailable, "LEARNING_UNAVAILABLE") })
        {
            factory.Learning.Status = status; factory.Learning.ProblemCode = code;
            using var response = await DeleteAsync(client, factory.Learning.CourseId);
            Assert.Equal(status == HttpStatusCode.ServiceUnavailable ? HttpStatusCode.BadGateway : status, response.StatusCode);
            Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        }
    }

    [Fact(DisplayName = nameof(ReaderMissingIntentAndInvalidCsrfNeverReachLearning))]
    public async Task ReaderMissingIntentAndInvalidCsrfNeverReachLearning()
    {
        using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        using var missing = await DeleteAsync(client, factory.Learning.CourseId, null); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        using var csrf = await DeleteAsync(client, factory.Learning.CourseId); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf-course"); factory.Identity.Permissions = ["autoria.ler"];
        using var denied = await DeleteAsync(client, factory.Learning.CourseId); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, factory.Learning.Calls);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid courseId, string? key = "delete-intent")
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/courses/{courseId}");
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Cancellation);
    }
}
