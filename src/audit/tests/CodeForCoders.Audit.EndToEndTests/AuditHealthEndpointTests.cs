using System.Net;
using Xunit;

namespace CodeForCoders.Audit.EndToEndTests;

[Collection(AuditApiCollection.Name)]
public sealed class AuditHealthEndpointTests(AuditApiFactory factory)
{
    [Fact]
    public async Task LiveHealthEndpointConfirmsAuditProcessIsAlive()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
