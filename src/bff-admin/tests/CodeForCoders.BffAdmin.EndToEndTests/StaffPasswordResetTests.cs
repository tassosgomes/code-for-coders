using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Contracts;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class StaffPasswordResetTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(ResetStaffPassword_ForwardsTokenPasswordAndIdempotencyKey))]
    [Trait("Layer", "BffAdmin staff password reset - EndToEnd")]
    public async Task ResetStaffPassword_ForwardsTokenPasswordAndIdempotencyKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.IdentityHandler.Reset();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-password-resets")
        {
            Content = JsonContent.Create(new StaffPasswordResetRequestV1("reset-secret", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", "bff-staff-reset-1");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new Uri("http://identity.test/internal/v1/staff-password-resets"), factory.IdentityHandler.LastRequestUri);
        Assert.Equal(new StaffPasswordResetRequestV1("reset-secret", "SenhaForte1!"), factory.IdentityHandler.LastRequest);
        Assert.Equal("bff-staff-reset-1", factory.IdentityHandler.LastIdempotencyKey);
        AssertServiceAssertionIsValid(factory.IdentityHandler.LastAssertion!, factory.IdentityPublicKeyBase64);
    }

    [Fact(DisplayName = nameof(ResetStaffPassword_MapsIdentityIdempotencyConflictToPublicContract))]
    [Trait("Layer", "BffAdmin staff password reset - EndToEnd")]
    public async Task ResetStaffPassword_MapsIdentityIdempotencyConflictToPublicContract()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.IdentityHandler.Reset();
        factory.IdentityHandler.ResponseStatus = HttpStatusCode.UnprocessableEntity;
        factory.IdentityHandler.ResponseCode = "IDEMPOTENCY_CONFLICT";
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-password-resets")
        {
            Content = JsonContent.Create(new StaffPasswordResetRequestV1("reset-secret", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", "bff-staff-reset-conflict-1");
        using var response = await client.SendAsync(request, cancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", problem.RootElement.GetProperty("code").GetString());
    }

    private static void AssertServiceAssertionIsValid(string assertion, string encodedPublicKey)
    {
        var segments = assertion.Split('.');
        Assert.Equal(3, segments.Length);
        using var header = JsonDocument.Parse(DecodeBase64Url(segments[0]));
        using var claims = JsonDocument.Parse(DecodeBase64Url(segments[1]));
        Assert.Equal("e2e-test", header.RootElement.GetProperty("kid").GetString());
        Assert.Equal("bff-admin", claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal("identity-internal", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal("bff-admin", claims.RootElement.GetProperty("sub").GetString());
        Assert.Equal("staff-passwords:reset", claims.RootElement.GetProperty("scope").GetString());

        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(encodedPublicKey), out _);
        var signingInput = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
        Assert.True(rsa.VerifyData(
            signingInput,
            DecodeBase64Url(segments[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64 + new string('=', (4 - base64.Length % 4) % 4));
    }
}
