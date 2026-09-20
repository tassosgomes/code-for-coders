using System.Net;
using System.Net.Http.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Identity.EndToEndTests;

[Collection(IdentityApiCollection.Name)]
public sealed class PlatformHeartbeatEndpointTests(IdentityApiFactory factory)
{
    [Fact]
    public async Task PostHeartbeat_ReturnsAcceptedAndPersistsOutboxMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/platform/heartbeat")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(cancellationToken);
        Assert.NotNull(responseBody);
        Assert.Equal(tenantId, responseBody!.TenantId);
        Assert.Equal("identity.platform.heartbeat.v1", responseBody.RoutingKey);

        await using var scope = factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(tenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var messages = await dbContext.OutboxMessages.ToListAsync(cancellationToken);

        var message = Assert.Single(messages);
        Assert.Equal(responseBody.EventId, message.Id);
        Assert.Equal(tenantId, message.TenantId);
        Assert.Null(message.ProcessedOn);
        Assert.Equal("identity.platform.heartbeat.v1", message.RoutingKey);
    }

    private sealed record HeartbeatResponse(
        Guid EventId,
        Guid TenantId,
        DateTimeOffset OccurredOn,
        string RoutingKey);
}
