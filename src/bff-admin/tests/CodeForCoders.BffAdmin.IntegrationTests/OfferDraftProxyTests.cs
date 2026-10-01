using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class OfferDraftProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string Path(string method) => method == "POST"
        ? $"/api/v1/catalog/courses/{Guid.CreateVersion7():D}/offers" : $"/api/v1/catalog/offers/{Guid.CreateVersion7():D}";
    private static object Terms => new { name = "Draft", priceCents = 49700, accessPeriod = new { type = "months", months = 12 } };

    [Theory(DisplayName = nameof(WritesForwardServerJwtKeyBodyAndContractStatus))]
    [InlineData("POST", HttpStatusCode.Created)]
    [InlineData("PATCH", HttpStatusCode.OK)]
    [InlineData("DELETE", HttpStatusCode.NoContent)]
    public async Task WritesForwardServerJwtKeyBodyAndContractStatus(string method, HttpStatusCode status)
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Authorization = new("Bearer", "browser-token");
        var path = Path(method); using var response = await SendAsync(client, method, path, "offer-intent");
        Assert.Equal(status, response.StatusCode); Assert.Equal("commerce", factory.Identity.LastAudience);
        Assert.Equal("server-commerce-token", factory.Commerce.Token); Assert.Equal("offer-intent", factory.Commerce.IdempotencyKey);
        Assert.Equal(method, factory.Commerce.Method!.Method); Assert.Equal(path.Replace("/api/", "/internal/"), factory.Commerce.Uri!.AbsolutePath);
        if (method == "DELETE") Assert.Null(factory.Commerce.Body);
        else { Assert.Contains("49700", factory.Commerce.Body); Assert.Contains("months", factory.Commerce.Body); }
        if (method == "POST")
        {
            var offer = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.Equal($"/api/v1/catalog/offers/{offer.GetProperty("offerId").GetGuid():D}", response.Headers.Location!.OriginalString);
        }
    }

    [Theory(DisplayName = nameof(DomainErrorsPreserveStatusCodeAndFieldDetail))]
    [InlineData("POST", HttpStatusCode.NotFound, "CATALOG_COURSE_NOT_FOUND")]
    [InlineData("PATCH", HttpStatusCode.NotFound, "OFFER_NOT_FOUND")]
    [InlineData("PATCH", HttpStatusCode.UnprocessableEntity, "FIELD_INVALID")]
    [InlineData("POST", HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED")]
    [InlineData("DELETE", HttpStatusCode.UnprocessableEntity, "OFFER_STATE_CONFLICT")]
    public async Task DomainErrorsPreserveStatusCodeAndFieldDetail(string method, HttpStatusCode status, string code)
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        factory.Commerce.Status = status; factory.Commerce.ErrorCode = code; factory.Commerce.ErrorDetail = "priceCents must be between 1 and 9999999.";
        using var client = await factory.AuthenticatedAsync(); using var response = await SendAsync(client, method, Path(method), "error");
        Assert.Equal(status, response.StatusCode); var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(code, problem.GetProperty("code").GetString()); Assert.Equal(factory.Commerce.ErrorDetail, problem.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = nameof(MissingSessionPermissionKeyAndCsrfBlockWrites))]
    public async Task MissingSessionPermissionKeyAndCsrfBlockWrites()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var anonymous = factory.CreateClient(); using var absent = await SendAsync(anonymous, "POST", Path("POST"), "key"); Assert.Equal(HttpStatusCode.Unauthorized, absent.StatusCode);
        using var client = await factory.AuthenticatedAsync(); using var missingKey = await SendAsync(client, "DELETE", Path("DELETE")); Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token"); using var csrf = await SendAsync(client, "POST", Path("POST"), "key"); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        factory.Identity.Permissions = []; using var permission = await SendAsync(client, "PATCH", Path("PATCH"), "key"); Assert.Equal(HttpStatusCode.Forbidden, permission.StatusCode);
        Assert.Equal(0, factory.Commerce.Calls);
    }

    [Theory(DisplayName = nameof(UnavailableAndTimedOutWritesReturnGatewayErrors))]
    [InlineData(false, HttpStatusCode.BadGateway)]
    [InlineData(true, HttpStatusCode.GatewayTimeout)]
    public async Task UnavailableAndTimedOutWritesReturnGatewayErrors(bool timeout, HttpStatusCode status)
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        factory.Commerce.Timeout = timeout; factory.Commerce.Unavailable = !timeout;
        using var client = await factory.AuthenticatedAsync(); using var failed = await SendAsync(client, "POST", Path("POST"), "same-intent"); Assert.Equal(status, failed.StatusCode);
        factory.Commerce.Timeout = false; factory.Commerce.Unavailable = false;
        using var retried = await SendAsync(client, "POST", Path("POST"), "same-intent"); Assert.Equal(HttpStatusCode.Created, retried.StatusCode); Assert.Equal("same-intent", factory.Commerce.IdempotencyKey);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, string? key = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "DELETE") request.Content = JsonContent.Create(Terms);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Cancellation);
    }
}
