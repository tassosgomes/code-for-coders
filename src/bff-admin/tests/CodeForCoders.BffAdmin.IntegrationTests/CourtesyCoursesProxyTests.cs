using System.Net;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyCoursesProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealClientUsesCommerceAudienceAndActorTokenAndEscapesTitle))]
    public async Task RealClientUsesCommerceAudienceAndActorTokenAndEscapesTitle()
    {
        await using var factory = new CourtesyBffApiFactory(); factory.Identity.Permissions = ["cortesia.conceder"];
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Authorization = new("Bearer", "browser-token");
        using var response = await client.GetAsync("/api/v1/courtesy-courses?_page=2&_size=10&title=A%C3%A7%C3%A3o%20%26%20C%23", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.IsType<CourtesyCoursesClient>(factory.Services.GetRequiredService<ICourtesyCoursesClient>());
        Assert.Equal("commerce", factory.Identity.LastAudience); Assert.Equal("server-commerce-token", factory.Courses.Token);
        Assert.Equal("/internal/v1/courtesy-courses", factory.Courses.Uri!.AbsolutePath);
        Assert.Equal("?_page=2&_size=10&title=A%C3%A7%C3%A3o%20%26%20C%23", factory.Courses.Uri.Query);
        Assert.Contains("Course without an offer", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MissingPermissionCannotReachCommerce))]
    public async Task MissingPermissionCannotReachCommerce()
    {
        await using var factory = new CourtesyBffApiFactory(); factory.Identity.Permissions = ["oferta.editar"];
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync("/api/v1/courtesy-courses", Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(0, factory.Courses.Calls);
        Assert.Contains("PERMISSION_DENIED", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MissingRevokedAndPermissionRemovedSessionsStopAtIdentity))]
    public async Task MissingRevokedAndPermissionRemovedSessionsStopAtIdentity()
    {
        await using var factory = new CourtesyBffApiFactory(); factory.Identity.Permissions = ["cortesia.conceder"];
        using var anonymous = factory.CreateClient();
        using var missing = await anonymous.GetAsync("/api/v1/courtesy-courses", Cancellation); Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var client = await factory.AuthenticatedAsync();
        using var first = await client.GetAsync("/api/v1/courtesy-courses", Cancellation); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        factory.Identity.Permissions = [];
        using var removed = await client.GetAsync("/api/v1/courtesy-courses", Cancellation); Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
        factory.Identity.Revoked = true;
        using var revoked = await client.GetAsync("/api/v1/courtesy-courses", Cancellation); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Equal(1, factory.Courses.Calls);
    }

    [Fact(DisplayName = nameof(UpstreamFailuresUseTheCourtesyContractCodes))]
    public async Task UpstreamFailuresUseTheCourtesyContractCodes()
    {
        await using var factory = new CourtesyBffApiFactory(); factory.Identity.Permissions = ["cortesia.conceder"];
        using var client = await factory.AuthenticatedAsync(); factory.Courses.Malformed = true;
        using var malformed = await client.GetAsync("/api/v1/courtesy-courses", Cancellation);
        Assert.Equal(HttpStatusCode.BadGateway, malformed.StatusCode); Assert.Contains("COMMERCE_UNAVAILABLE", await malformed.Content.ReadAsStringAsync(Cancellation));
        factory.Courses.Malformed = false; factory.Courses.Unavailable = true;
        using var unavailable = await client.GetAsync("/api/v1/courtesy-courses", Cancellation);
        Assert.Equal(HttpStatusCode.BadGateway, unavailable.StatusCode); Assert.Contains("COMMERCE_UNAVAILABLE", await unavailable.Content.ReadAsStringAsync(Cancellation));
        factory.Courses.Unavailable = false; factory.Courses.Timeout = true;
        using var timeout = await client.GetAsync("/api/v1/courtesy-courses", Cancellation);
        Assert.Equal(HttpStatusCode.GatewayTimeout, timeout.StatusCode); Assert.Contains("UPSTREAM_TIMEOUT", await timeout.Content.ReadAsStringAsync(Cancellation));
    }

    [Theory(DisplayName = nameof(InvalidQueriesDoNotReachCommerce))]
    [InlineData("?_page=0")]
    [InlineData("?_size=51")]
    [InlineData("?title=")]
    public async Task InvalidQueriesDoNotReachCommerce(string query)
    {
        await using var factory = new CourtesyBffApiFactory(); factory.Identity.Permissions = ["cortesia.conceder"];
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync("/api/v1/courtesy-courses" + query, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, factory.Courses.Calls);
    }
}
