using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CatalogCourseRecordProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly string Path = $"/api/v1/catalog/courses/{Guid.CreateVersion7():D}";

    [Fact(DisplayName = nameof(ReadAndPatchForwardServerJwtAndIdempotencyKey))]
    public async Task ReadAndPatchForwardServerJwtAndIdempotencyKey()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Authorization = new("Bearer", "browser-token");
        using var read = await client.GetAsync(Path, Cancellation); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "record-intent");
        using var patch = await client.PatchAsJsonAsync(Path, new { tagline = "Saved" }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode); Assert.Contains("Saved", await patch.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("commerce", factory.Identity.LastAudience); Assert.Equal("server-commerce-token", factory.Commerce.Token);
        Assert.Equal("record-intent", factory.Commerce.IdempotencyKey); Assert.Equal(HttpMethod.Patch, factory.Commerce.Method);
        Assert.Equal("{\"tagline\":\"Saved\"}", factory.Commerce.Body); Assert.Equal(Path.Replace("/api/", "/internal/"), factory.Commerce.Uri!.AbsolutePath);
    }

    [Fact(DisplayName = nameof(MissingSessionPermissionKeyAndCsrfNeverReachCommerce))]
    public async Task MissingSessionPermissionKeyAndCsrfNeverReachCommerce()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var anonymous = factory.CreateClient(); using var missing = await anonymous.GetAsync(Path, Cancellation); Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var client = await factory.AuthenticatedAsync();
        using var missingKey = await client.PatchAsJsonAsync(Path, new { tagline = "Saved" }, Cancellation); Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token"); client.DefaultRequestHeaders.Add("Idempotency-Key", "key");
        using var csrf = await client.PatchAsJsonAsync(Path, new { tagline = "Saved" }, Cancellation); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        factory.Identity.Permissions = [];
        using var forbidden = await client.GetAsync(Path, Cancellation); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, factory.Commerce.Calls);
    }

    [Theory(DisplayName = nameof(DomainErrorsPreserveStatusCodeFieldAndLimit))]
    [InlineData(HttpStatusCode.NotFound, "CATALOG_COURSE_NOT_FOUND")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "FIELD_INVALID")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED")]
    public async Task DomainErrorsPreserveStatusCodeFieldAndLimit(HttpStatusCode status, string code)
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        factory.Commerce.Status = status; factory.Commerce.ErrorCode = code;
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Add("Idempotency-Key", "invalid");
        using var response = await client.PatchAsJsonAsync(Path, new { tagline = new string('a', 161) }, Cancellation);
        Assert.Equal(status, response.StatusCode); var problem = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.Contains(code, problem); Assert.Contains("tagline", problem); Assert.Contains("160", problem);
    }

    [Fact(DisplayName = nameof(TimedOutWriteReturns504AndRetryForwardsTheSameIntent))]
    public async Task TimedOutWriteReturns504AndRetryForwardsTheSameIntent()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"]; factory.Commerce.Timeout = true;
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Add("Idempotency-Key", "same-intent");
        using var timeout = await client.PatchAsJsonAsync(Path, new { tagline = "Saved" }, Cancellation);
        Assert.Equal(HttpStatusCode.GatewayTimeout, timeout.StatusCode); Assert.Equal(1, factory.Commerce.Calls);
        Assert.Contains("COMMERCE_TIMEOUT", await timeout.Content.ReadAsStringAsync(Cancellation));
        factory.Commerce.Timeout = false;
        using var retried = await client.PatchAsJsonAsync(Path, new { tagline = "Saved" }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode); Assert.Equal("same-intent", factory.Commerce.IdempotencyKey); Assert.Equal(2, factory.Commerce.Calls);
    }

    [Fact(DisplayName = nameof(MalformedRecordReturns502AndRevokedPermissionBlocksTheNextRead))]
    public async Task MalformedRecordReturns502AndRevokedPermissionBlocksTheNextRead()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"]; factory.Commerce.Malformed = true;
        using var client = await factory.AuthenticatedAsync(); using var malformed = await client.GetAsync(Path, Cancellation);
        Assert.Equal(HttpStatusCode.BadGateway, malformed.StatusCode);
        factory.Identity.Permissions = []; using var revoked = await client.GetAsync(Path, Cancellation); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.Equal(1, factory.Commerce.Calls);
    }

    [Theory(DisplayName = nameof(IdempotencyKeyUsesContractLengthLimit))]
    [InlineData(128, HttpStatusCode.OK, 1)]
    [InlineData(129, HttpStatusCode.BadRequest, 0)]
    public async Task IdempotencyKeyUsesContractLengthLimit(int length, HttpStatusCode status, int calls)
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Add("Idempotency-Key", new string('k', length));
        using var response = await client.PatchAsJsonAsync(Path, new { tagline = "Saved" }, Cancellation);
        Assert.Equal(status, response.StatusCode); Assert.Equal(calls, factory.Commerce.Calls);
    }
}
