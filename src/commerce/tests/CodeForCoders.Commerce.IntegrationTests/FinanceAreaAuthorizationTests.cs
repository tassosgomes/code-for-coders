using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class FinanceAreaAuthorizationTests
{
    [Fact(DisplayName = nameof(FinanceArea_RejectsMissingToken))]
    [Trait("Layer", "Commerce finance area - Integration")]
    public async Task FinanceArea_RejectsMissingToken()
    {
        using var factory = new FinanceAreaApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/internal/v1/finance-area", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "TOKEN_INVALID");
    }

    [Fact(DisplayName = nameof(FinanceArea_RejectsTokenSignedByUnknownKey))]
    [Trait("Layer", "Commerce finance area - Integration")]
    public async Task FinanceArea_RejectsTokenSignedByUnknownKey()
    {
        using var factory = new FinanceAreaApiFactory();
        using var client = factory.CreateClient();
        using var unknownKey = RSA.Create(2048);

        using var response = await GetWithTokenAsync(
            client,
            CreateToken(new RsaSecurityKey(unknownKey) { KeyId = "unknown-key" }, "unknown-key"));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "TOKEN_INVALID");
    }

    [Fact(DisplayName = nameof(FinanceArea_RejectsTokenForAnotherAudience))]
    [Trait("Layer", "Commerce finance area - Integration")]
    public async Task FinanceArea_RejectsTokenForAnotherAudience()
    {
        using var factory = new FinanceAreaApiFactory();
        using var client = factory.CreateClient();

        using var response = await GetWithTokenAsync(
            client,
            CreateToken(factory.JwksHandler.SigningKey, "commerce-test-key", audience: "identity"));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "TOKEN_INVALID");
    }

    [Fact(DisplayName = nameof(FinanceArea_RejectsExpiredToken))]
    [Trait("Layer", "Commerce finance area - Integration")]
    public async Task FinanceArea_RejectsExpiredToken()
    {
        using var factory = new FinanceAreaApiFactory();
        using var client = factory.CreateClient();

        using var response = await GetWithTokenAsync(
            client,
            CreateToken(
                factory.JwksHandler.SigningKey,
                "commerce-test-key",
                expires: DateTime.UtcNow.AddMinutes(-1)));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "TOKEN_INVALID");
    }

    [Fact(DisplayName = nameof(FinanceArea_RejectsValidTokenWithoutFinancePermission))]
    [Trait("Layer", "Commerce finance area - Integration")]
    public async Task FinanceArea_RejectsValidTokenWithoutFinancePermission()
    {
        using var factory = new FinanceAreaApiFactory();
        using var client = factory.CreateClient();

        using var response = await GetWithTokenAsync(
            client,
            CreateToken(factory.JwksHandler.SigningKey, "commerce-test-key", permission: "autoria.ler"));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
    }

    [Fact(DisplayName = nameof(FinanceArea_ReturnsReservedAreaForAuthorizedActor))]
    [Trait("Layer", "Commerce finance area - Integration")]
    public async Task FinanceArea_ReturnsReservedAreaForAuthorizedActor()
    {
        using var factory = new FinanceAreaApiFactory();
        using var client = factory.CreateClient();

        using var response = await GetWithTokenAsync(
            client,
            CreateToken(factory.JwksHandler.SigningKey, "commerce-test-key"));
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Unexpected response: {responseBody}");
        using var content = JsonDocument.Parse(responseBody);

        Assert.Equal("reserved", content.RootElement.GetProperty("status").GetString());
    }

    [Fact(DisplayName = nameof(FinanceArea_FailsClosedWhenJwksIsUnavailableWithoutFailingHealth))]
    [Trait("Layer", "Commerce finance area - Integration")]
    public async Task FinanceArea_FailsClosedWhenJwksIsUnavailableWithoutFailingHealth()
    {
        using var factory = new FinanceAreaApiFactory();
        factory.JwksHandler.IsUnavailable = true;
        using var client = factory.CreateClient();

        using var response = await GetWithTokenAsync(
            client,
            CreateToken(factory.JwksHandler.SigningKey, "commerce-test-key"));
        using var healthResponse = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "TOKEN_INVALID");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetWithTokenAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/v1/finance-area");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string CreateToken(
        SecurityKey signingKey,
        string keyId,
        string audience = "commerce",
        string permission = "financeiro.ler",
        DateTime? expires = null)
    {
        signingKey.KeyId = keyId;
        var now = DateTime.UtcNow;
        var expiresAt = expires ?? now.AddMinutes(2);
        var token = new JwtSecurityToken(
            "identity",
            audience,
            [
                new Claim("sub", "7a8b9c0d-1e2f-4a3b-9c4d-5e6f7a8b9c0d"),
                new Claim("roles", "financeiro"),
                new Claim("permissions", permission),
            ],
            expiresAt < now ? expiresAt.AddMinutes(-2) : now.AddMinutes(-1),
            expiresAt,
            new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == status, $"Expected {status}, received {response.StatusCode}: {body}");
        Assert.False(string.IsNullOrWhiteSpace(body), $"Expected a problem document, received an empty body with {response.StatusCode}.");
        using var document = JsonDocument.Parse(body);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
    }
}
