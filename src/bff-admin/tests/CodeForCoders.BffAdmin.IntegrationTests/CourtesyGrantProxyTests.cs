using System.Net;
using System.Net.Http.Json;
using Xunit;
namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyGrantProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static CourtesyBffApiFactory Factory() { var factory = new CourtesyBffApiFactory(); factory.Identity.Roles = ["financeiro"]; factory.Identity.Permissions = ["financeiro.ler", "cortesia.conceder"]; return factory; }
    private static object Body() => new { studentId = Guid.CreateVersion7(), courseId = Guid.CreateVersion7(), accessPeriod = new { type = "months", months = 6 }, reason = "Bolsa de mentoria" };
    private static Task<HttpResponseMessage> GrantAsync(HttpClient client, string? key = "stable-key")
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/courtesy-grants") { Content = JsonContent.Create(Body()) };
        if (key is not null) message.Headers.Add("Idempotency-Key", key); return client.SendAsync(message, Cancellation);
    }
    [Fact(DisplayName = nameof(GrantAndReplayForwardActorTokenAndKeyAndReturnLocation))]
    public async Task GrantAndReplayForwardActorTokenAndKeyAndReturnLocation()
    {
        await using var factory = Factory(); using var client = await factory.AuthenticatedAsync(); using var response = await GrantAsync(client);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.Equal($"/api/v1/courtesy-grants/{factory.Grants.GrantId}", response.Headers.Location!.ToString());
        Assert.Equal("server-commerce-token", factory.Grants.Token); Assert.Equal("stable-key", factory.Grants.Key); Assert.Equal("commerce", factory.Identity.LastAudience);
        factory.Grants.Status = 200; using var replay = await GrantAsync(client); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.DoesNotContain(factory.Logs, log => log.Contains("Bolsa de mentoria", StringComparison.Ordinal) || log.Contains("stable-key", StringComparison.Ordinal));
    }
    [Fact(DisplayName = nameof(RequiredKeyCsrfSessionAndPermissionPreventUpstreamCalls))]
    public async Task RequiredKeyCsrfSessionAndPermissionPreventUpstreamCalls()
    {
        await using var factory = Factory(); using var client = await factory.AuthenticatedAsync();
        using var noKey = await GrantAsync(client, null); Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token"); using var csrf = await GrantAsync(client); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf-course"); factory.Identity.Permissions = ["financeiro.ler"];
        using var forbidden = await GrantAsync(client); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); Assert.Equal(0, factory.Grants.Calls);
        using var anonymous = factory.CreateClient(); using var missing = await GrantAsync(anonymous); Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
    }
    [Theory(DisplayName = nameof(RejectionCodesAndIdentityOutageAreMappedWithoutChangingIntent))]
    [InlineData(422, "FIELD_INVALID", 422)]
    [InlineData(422, "IDEMPOTENCY_KEY_REUSED", 422)]
    [InlineData(422, "COURSE_NOT_ELIGIBLE", 422)]
    [InlineData(422, "STUDENT_ACCOUNT_NOT_ELIGIBLE", 422)]
    [InlineData(503, "STUDENT_ACCOUNT_CHECK_UNAVAILABLE", 502)]
    public async Task RejectionCodesAndIdentityOutageAreMappedWithoutChangingIntent(int upstream, string code, int expected)
    {
        await using var factory = Factory(); factory.Grants.Status = upstream; factory.Grants.Code = code; using var client = await factory.AuthenticatedAsync();
        using var response = await GrantAsync(client); Assert.Equal(expected, (int)response.StatusCode); Assert.Contains(code, await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("stable-key", factory.Grants.Key); Assert.Equal(1, factory.Grants.Calls);
    }
    [Theory(DisplayName = nameof(UnavailableAndTimedOutCommerceUseContractErrors))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableAndTimedOutCommerceUseContractErrors(bool timeout)
    {
        await using var factory = Factory(); factory.Grants.Timeout = timeout; factory.Grants.Unavailable = !timeout;
        using var client = await factory.AuthenticatedAsync(); using var response = await GrantAsync(client);
        Assert.Equal(timeout ? 504 : 502, (int)response.StatusCode); Assert.Contains(timeout ? "UPSTREAM_TIMEOUT" : "COMMERCE_UNAVAILABLE", await response.Content.ReadAsStringAsync(Cancellation)); Assert.Equal(1, factory.Grants.Calls);
    }
    [Fact(DisplayName = nameof(PreviewAndGetUseCommerceAudienceAndPropagateNotFound))]
    public async Task PreviewAndGetUseCommerceAudienceAndPropagateNotFound()
    {
        await using var factory = Factory(); factory.Grants.Status = 200; using var client = await factory.AuthenticatedAsync();
        using var preview = await client.GetAsync("/api/v1/courtesy-term-preview?months=6", Cancellation); Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("?months=6", factory.Grants.Uri!.Query); Assert.Contains("2027-04-15", await preview.Content.ReadAsStringAsync(Cancellation));
        using var get = await client.GetAsync($"/api/v1/courtesy-grants/{factory.Grants.GrantId}", Cancellation); Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        factory.Grants.Status = 404; factory.Grants.Code = "GRANT_NOT_FOUND"; using var hidden = await client.GetAsync($"/api/v1/courtesy-grants/{Guid.CreateVersion7()}", Cancellation); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); Assert.Contains("GRANT_NOT_FOUND", await hidden.Content.ReadAsStringAsync(Cancellation));
    }
}
