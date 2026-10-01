using System.Net;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CatalogCoursesProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealClientUsesCommerceAudienceAndServerTokenWithPagination))]
    public async Task RealClientUsesCommerceAudienceAndServerTokenWithPagination()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Authorization = new("Bearer", "browser-token");
        using var response = await client.GetAsync("/api/v1/catalog/courses?_page=2&_size=10", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.IsType<CommerceCatalogClient>(factory.Services.GetRequiredService<ICommerceCatalogClient>());
        Assert.Equal("commerce", factory.Identity.LastAudience); Assert.Equal("server-commerce-token", factory.Commerce.Token);
        Assert.Equal("?_page=2&_size=10", factory.Commerce.Uri!.Query);
        Assert.Contains("Catalog course", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Theory(DisplayName = nameof(OtherRolesCannotReachCommerce))]
    [InlineData("administrador", "acesso.gerir")]
    [InlineData("professor", "autoria.editar")]
    [InlineData("suporte", "suporte.atender")]
    public async Task OtherRolesCannotReachCommerce(string role, string permission)
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Roles = [role]; factory.Identity.Permissions = [permission];
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync("/api/v1/catalog/courses", Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(0, factory.Commerce.Calls);
    }

    [Fact(DisplayName = nameof(RemovingRoleRefusesTheNextActionOfTheOpenSession))]
    public async Task RemovingRoleRefusesTheNextActionOfTheOpenSession()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync();
        using var first = await client.GetAsync("/api/v1/catalog/courses", Cancellation); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        factory.Identity.Permissions = [];
        using var removed = await client.GetAsync("/api/v1/catalog/courses", Cancellation); Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
        factory.Identity.Revoked = true;
        using var revoked = await client.GetAsync("/api/v1/catalog/courses", Cancellation); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Equal(1, factory.Commerce.Calls);
    }

    [Fact(DisplayName = nameof(CommerceFailureAndTimeoutUseContractErrors))]
    public async Task CommerceFailureAndTimeoutUseContractErrors()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync(); factory.Commerce.Malformed = true;
        using var unavailable = await client.GetAsync("/api/v1/catalog/courses", Cancellation);
        Assert.Equal(HttpStatusCode.BadGateway, unavailable.StatusCode); Assert.Contains("COMMERCE_UNAVAILABLE", await unavailable.Content.ReadAsStringAsync(Cancellation));
        factory.Commerce.Malformed = false; factory.Commerce.Timeout = true;
        using var timeout = await client.GetAsync("/api/v1/catalog/courses", Cancellation);
        Assert.Equal(HttpStatusCode.GatewayTimeout, timeout.StatusCode); Assert.Contains("COMMERCE_TIMEOUT", await timeout.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MissingSessionAndInvalidPaginationDoNotReachCommerce))]
    public async Task MissingSessionAndInvalidPaginationDoNotReachCommerce()
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var anonymous = factory.CreateClient(); using var missing = await anonymous.GetAsync("/api/v1/catalog/courses", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var client = await factory.AuthenticatedAsync(); using var invalid = await client.GetAsync("/api/v1/catalog/courses?_size=51", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.Equal(0, factory.Commerce.Calls);
    }

    [Theory(DisplayName = nameof(CommerceAuthorizationErrorsRemain401And403))]
    [InlineData(HttpStatusCode.Unauthorized, "TOKEN_INVALID")]
    [InlineData(HttpStatusCode.Forbidden, "PERMISSION_DENIED")]
    public async Task CommerceAuthorizationErrorsRemain401And403(HttpStatusCode status, string code)
    {
        await using var factory = new CourseBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"]; factory.Commerce.Status = status;
        using var client = await factory.AuthenticatedAsync(); using var response = await client.GetAsync("/api/v1/catalog/courses", Cancellation);
        Assert.Equal(status, response.StatusCode); Assert.Contains(code, await response.Content.ReadAsStringAsync(Cancellation));
    }
}
