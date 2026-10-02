using System.Net;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyStudentGrantsProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static CourtesyBffApiFactory Factory()
    {
        var factory = new CourtesyBffApiFactory(); factory.Identity.Roles = ["financeiro"]; factory.Identity.Permissions = ["financeiro.ler", "cortesia.conceder"];
        factory.Grants.Status = 200; return factory;
    }
    [Fact(DisplayName = nameof(ListForwardsActorTokenStudentAndPaginationWithNoMutationHeaders))]
    public async Task ListForwardsActorTokenStudentAndPaginationWithNoMutationHeaders()
    {
        await using var factory = Factory(); using var client = await factory.AuthenticatedAsync(); var student = Guid.CreateVersion7();
        using var response = await client.GetAsync($"/api/v1/students/{student}/access-grants?_page=2&_size=1", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal($"/internal/v1/students/{student}/access-grants", factory.Grants.Uri!.AbsolutePath);
        Assert.Equal("?_page=2&_size=1", factory.Grants.Uri.Query); Assert.Equal("server-commerce-token", factory.Grants.Token); Assert.Equal("commerce", factory.Identity.LastAudience);
        Assert.Null(factory.Grants.Key); Assert.Null(factory.Grants.Body);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("expired", doc.RootElement.GetProperty("data")[0].GetProperty("status").GetString()); Assert.Equal(2, doc.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
    }
    [Fact(DisplayName = nameof(SessionPermissionAndInvalidPaginationStopCommerceCalls))]
    public async Task SessionPermissionAndInvalidPaginationStopCommerceCalls()
    {
        await using var factory = Factory(); var path = $"/api/v1/students/{Guid.CreateVersion7()}/access-grants";
        using var anonymous = factory.CreateClient(); using var missing = await anonymous.GetAsync(path, Cancellation); Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var client = await factory.AuthenticatedAsync(); factory.Identity.Permissions = ["financeiro.ler"];
        using var forbidden = await client.GetAsync(path, Cancellation); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        factory.Identity.Permissions = ["financeiro.ler", "cortesia.conceder"];
        foreach (var query in new[] { "?_page=0", "?_size=51", "?_page=2147483647&_size=50" })
        {
            using var invalid = await client.GetAsync(path + query, Cancellation); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        Assert.Equal(0, factory.Grants.Calls);
    }
    [Theory(DisplayName = nameof(UpstreamFailureUses502Or504Contract))]
    [InlineData("unavailable", 502, "COMMERCE_UNAVAILABLE")]
    [InlineData("timeout", 504, "UPSTREAM_TIMEOUT")]
    [InlineData("malformed", 502, "COMMERCE_UNAVAILABLE")]
    public async Task UpstreamFailureUses502Or504Contract(string mode, int status, string code)
    {
        await using var factory = Factory(); factory.Grants.Unavailable = mode == "unavailable"; factory.Grants.Timeout = mode == "timeout"; factory.Grants.Malformed = mode == "malformed";
        using var client = await factory.AuthenticatedAsync(); using var response = await client.GetAsync($"/api/v1/students/{Guid.CreateVersion7()}/access-grants", Cancellation);
        Assert.Equal(status, (int)response.StatusCode); Assert.Contains(code, await response.Content.ReadAsStringAsync(Cancellation));
    }
}
