using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class OfferUnpublishProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string Path => $"/api/v1/catalog/offers/{Guid.CreateVersion7():D}/unpublish";

    [Fact(DisplayName = nameof(UnpublishForwardsServerJwtAndKeyWithNoBody))]
    public async Task UnpublishForwardsServerJwtAndKeyWithNoBody()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Authorization = new("Bearer", "browser-token");
        var path = Path; using var request = new HttpRequestMessage(HttpMethod.Post, path); request.Headers.Add("Idempotency-Key", "unpublish-intent");
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("commerce", factory.Identity.LastAudience);
        Assert.Equal("server-commerce-token", factory.Commerce.Token); Assert.Equal("unpublish-intent", factory.Commerce.IdempotencyKey);
        Assert.Equal(path.Replace("/api/", "/internal/"), factory.Commerce.Uri!.AbsolutePath); Assert.Null(factory.Commerce.Body);
        Assert.Equal("unpublished", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString());
    }

    [Theory(DisplayName = nameof(UnpublishPreservesCommerceErrorCode))]
    [InlineData("OFFER_STATE_CONFLICT")]
    [InlineData("IDEMPOTENCY_KEY_REUSED")]
    public async Task UnpublishPreservesCommerceErrorCode(string code)
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        factory.Commerce.Status = HttpStatusCode.UnprocessableEntity; factory.Commerce.ErrorCode = code;
        using var client = await factory.AuthenticatedAsync(); using var request = new HttpRequestMessage(HttpMethod.Post, Path);
        request.Headers.Add("Idempotency-Key", "unpublish-error"); using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(MissingKeyAndPermissionPreventUnpublish))]
    public async Task MissingKeyAndPermissionPreventUnpublish()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync(); using var missing = await client.PostAsync(Path, null, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode); Assert.Equal(0, factory.Commerce.Calls);
        factory.Identity.Permissions = []; using var request = new HttpRequestMessage(HttpMethod.Post, Path); request.Headers.Add("Idempotency-Key", "forbidden");
        using var denied = await client.SendAsync(request, Cancellation); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal(0, factory.Commerce.Calls);
    }
}
