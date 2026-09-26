using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class StaffPasswordRecoveryRequestTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(RequestStaffPasswordReset_ForwardsEmailAndReturnsAccepted))]
    [Trait("Layer", "BffAdmin staff password recovery request - EndToEnd")]
    public async Task RequestStaffPasswordReset_ForwardsEmailAndReturnsAccepted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.IdentityHandler.Reset();
        factory.IdentityHandler.ResponseStatus = HttpStatusCode.Accepted;
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-password-reset-requests")
        {
            Content = JsonContent.Create(new StaffPasswordRecoveryRequestV1("marina@example.com")),
        };
        request.Headers.Add("Idempotency-Key", "bff-staff-recovery-1");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        Assert.Equal(new Uri("http://identity.test/internal/v1/staff-password-reset-requests"), factory.IdentityHandler.LastRequestUri);
        Assert.Equal(new StaffPasswordRecoveryRequestV1("marina@example.com"), factory.IdentityHandler.LastRecoveryRequest);
        Assert.Equal("bff-staff-recovery-1", factory.IdentityHandler.LastIdempotencyKey);
        using var claims = JsonDocument.Parse(DecodeBase64Url(factory.IdentityHandler.LastAssertion!.Split('.')[1]));
        Assert.Equal("staff-passwords:reset", claims.RootElement.GetProperty("scope").GetString());
    }

    [Fact(DisplayName = nameof(RequestStaffPasswordReset_MapsIdentityIdempotencyConflictToPublicContract))]
    [Trait("Layer", "BffAdmin staff password recovery request - EndToEnd")]
    public async Task RequestStaffPasswordReset_MapsIdentityIdempotencyConflictToPublicContract()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.IdentityHandler.Reset();
        factory.IdentityHandler.ResponseStatus = HttpStatusCode.UnprocessableEntity;
        factory.IdentityHandler.ResponseCode = "IDEMPOTENCY_CONFLICT";
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-password-reset-requests")
        {
            Content = JsonContent.Create(new StaffPasswordRecoveryRequestV1("marina@example.com")),
        };
        request.Headers.Add("Idempotency-Key", "bff-staff-recovery-conflict-1");

        using var response = await client.SendAsync(request, cancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", problem.RootElement.GetProperty("code").GetString());
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64 + new string('=', (4 - base64.Length % 4) % 4));
    }
}
