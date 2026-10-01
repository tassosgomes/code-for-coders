using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using Xunit;

namespace CodeForCoders.BffStudent.IntegrationTests;

[Collection(BffStudentIntegrationCollection.Name)]
public sealed class ShowcaseAnonymousRouteTests(BffStudentIntegrationFixture fixture)
{
    private const string Route = "/api/v1/showcase/courses";
    private const string CardPage = """
        {"data":[{"courseId":"3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c","title":"Fundamentos de C#","level":"beginner",
        "summary":"Sintaxe, tipos e orientação a objetos.","lowestPriceCents":29700,"offerCount":1,"authorEmail":"leak@example.com"}],
        "pagination":{"page":1,"size":12,"total":1,"totalPages":1}}
        """;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(AnonymousVisitorGetsTheShowcaseSignedForCommerceOnly))]
    public async Task AnonymousVisitorGetsTheShowcaseSignedForCommerceOnly()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(HttpStatusCode.OK, CardPage);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Route, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var forwarded = Assert.Single(factory.Commerce.Requests);
        Assert.Equal(HttpMethod.Get, forwarded.Method);
        Assert.Equal("/internal/v1/showcase/courses", forwarded.RequestUri!.AbsolutePath);
        Assert.Equal("?_page=1&_size=12", forwarded.RequestUri.Query);
        Assert.False(forwarded.HasCookie);
        var assertion = ReadAssertion(forwarded.Authorization, factory.CommercePublicKey);
        Assert.Equal(ShowcaseBffFactory.CommerceKeyId, assertion.KeyId);
        Assert.Equal("bff-student", assertion.Claim("iss"));
        Assert.Equal("bff-student", assertion.Claim("sub"));
        Assert.Equal("commerce", assertion.Claim("aud"));
        Assert.Equal("showcase:read", assertion.Claim("scope"));
        Assert.Equal(ShowcaseBffFactory.TenantId, assertion.Claim("tenantId"));
        Assert.True(Guid.TryParse(assertion.Claim("jti"), out _));
        Assert.InRange(assertion.Lifetime, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));
        Assert.Equal(0, factory.Identity.Calls);
    }

    [Fact(DisplayName = nameof(ResponseKeepsOnlyTheContractFields))]
    public async Task ResponseKeepsOnlyTheContractFields()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(HttpStatusCode.OK, CardPage);
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync(Route, Cancellation);

        using var document = JsonDocument.Parse(body);
        var card = document.RootElement.GetProperty("data")[0];
        Assert.Equal(
            ["courseId", "level", "lowestPriceCents", "offerCount", "summary", "title"],
            card.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("leak@example.com", body, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(ValidCookieInvalidCookieAndNoCookieSeeTheSameShowcase))]
    public async Task ValidCookieInvalidCookieAndNoCookieSeeTheSameShowcase()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(HttpStatusCode.OK, CardPage);
        var validCookie = Guid.CreateVersion7().ToString("N");
        await factory.Services.GetRequiredService<IBffSessionStore>().StoreAsync(
            validCookie,
            new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Ana Souza", "csrf", DateTimeOffset.UtcNow.AddMinutes(30)),
            Cancellation);
        using var client = factory.CreateClient();

        var anonymous = await GetBodyAsync(client, null);
        var valid = await GetBodyAsync(client, validCookie);
        var invalid = await GetBodyAsync(client, "forged-or-expired-cookie");

        Assert.Equal(anonymous.Body, valid.Body);
        Assert.Equal(anonymous.Body, invalid.Body);
        Assert.All([anonymous, valid, invalid], result =>
        {
            Assert.Equal(HttpStatusCode.OK, result.Status);
            Assert.False(result.SetsCookie);
        });
        Assert.Equal(3, factory.Commerce.Requests.Count);
        Assert.All(factory.Commerce.Requests, request => Assert.False(request.HasCookie));
        Assert.Equal(0, factory.Identity.Calls);
    }

    [Fact(DisplayName = nameof(EveryReadCarriesItsOwnSingleUseAssertion))]
    public async Task EveryReadCarriesItsOwnSingleUseAssertion()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        using var client = factory.CreateClient();

        await client.GetAsync(Route, Cancellation);
        await client.GetAsync(Route, Cancellation);

        var identifiers = factory.Commerce.Requests
            .Select(request => ReadAssertion(request.Authorization, factory.CommercePublicKey).Claim("jti"))
            .ToArray();
        Assert.Equal(2, identifiers.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact(DisplayName = nameof(CommerceAssertionUsesItsOwnKeyAndNotTheIdentityKey))]
    public async Task CommerceAssertionUsesItsOwnKeyAndNotTheIdentityKey()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        using var client = factory.CreateClient();

        await client.GetAsync(Route, Cancellation);

        var authorization = Assert.Single(factory.Commerce.Requests).Authorization;
        Assert.True(ReadAssertion(authorization, factory.CommercePublicKey).SignatureIsValid);
        Assert.False(ReadAssertion(authorization, factory.IdentityPublicKey).SignatureIsValid);
    }

    [Fact(DisplayName = nameof(LevelAndPaginationAreForwardedToCommerce))]
    public async Task LevelAndPaginationAreForwardedToCommerce()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{Route}?level=beginner&_page=2&_size=6", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("?level=beginner&_page=2&_size=6", Assert.Single(factory.Commerce.Requests).RequestUri!.Query);
    }

    [Theory(DisplayName = nameof(InvalidQueryIsRefusedWithoutCallingCommerce))]
    [InlineData("?level=xyz")]
    [InlineData("?level=")]
    [InlineData("?_size=49")]
    [InlineData("?_size=0")]
    [InlineData("?_page=0")]
    public async Task InvalidQueryIsRefusedWithoutCallingCommerce(string query)
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{Route}{query}", Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("INVALID_REQUEST", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Empty(factory.Commerce.Requests);
    }

    [Theory(DisplayName = nameof(CommerceFailuresBecomeBadGatewayWithTheShowcaseCode))]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CommerceFailuresBecomeBadGatewayWithTheShowcaseCode(HttpStatusCode commerceStatus)
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => CommerceBoundaryHandler.Json(commerceStatus, """{"code":"SERVICE_ASSERTION_INVALID"}""");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Route, Cancellation);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.Contains("SHOWCASE_UNAVAILABLE", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SERVICE_ASSERTION_INVALID", body, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(RetryAfterATransientFailureSignsAFreshAssertion))]
    public async Task RetryAfterATransientFailureSignsAFreshAssertion()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        var attempts = 0;
        factory.Commerce.Respond = _ => Interlocked.Increment(ref attempts) == 1
            ? CommerceBoundaryHandler.Json(HttpStatusCode.ServiceUnavailable, "{}")
            : CommerceBoundaryHandler.Json(HttpStatusCode.OK, CardPage);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Route, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var identifiers = factory.Commerce.Requests
            .Select(request => ReadAssertion(request.Authorization, factory.CommercePublicKey).Claim("jti"))
            .ToArray();
        Assert.Equal(2, identifiers.Length);
        Assert.NotEqual(identifiers[0], identifiers[1]);
    }

    [Fact(DisplayName = nameof(CommerceTimeoutBecomesGatewayTimeoutWithTheShowcaseCode))]
    public async Task CommerceTimeoutBecomesGatewayTimeoutWithTheShowcaseCode()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => throw new TimeoutRejectedException("attempt timed out");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Route, Cancellation);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Contains("SHOWCASE_TIMEOUT", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(CommerceUnreachableBecomesBadGateway))]
    public async Task CommerceUnreachableBecomesBadGateway()
    {
        await using var factory = new ShowcaseBffFactory(fixture);
        factory.Commerce.Respond = _ => throw new HttpRequestException("connection refused");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Route, Cancellation);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("SHOWCASE_UNAVAILABLE", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    private static async Task<(HttpStatusCode Status, string Body, bool SetsCookie)> GetBodyAsync(HttpClient client, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"student_session={cookie}");
        }

        using var response = await client.SendAsync(request, Cancellation);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Cancellation), response.Headers.Contains("Set-Cookie"));
    }

    private static ReadAssertionResult ReadAssertion(string? authorization, byte[] publicKey)
    {
        Assert.StartsWith("Bearer ", authorization, StringComparison.Ordinal);
        var segments = authorization!["Bearer ".Length..].Split('.');
        Assert.Equal(3, segments.Length);
        using var header = JsonDocument.Parse(Decode(segments[0]));
        using var claims = JsonDocument.Parse(Decode(segments[1]));
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
        var valid = rsa.VerifyData(
            Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}"),
            Decode(segments[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var values = claims.RootElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.ValueKind == JsonValueKind.Number ? property.Value.GetInt64().ToString() : property.Value.GetString()!);
        var lifetime = TimeSpan.FromSeconds(long.Parse(values["exp"]) - long.Parse(values["iat"]));
        return new ReadAssertionResult(header.RootElement.GetProperty("kid").GetString()!, values, lifetime, valid);
    }

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - (padded.Length % 4)) % 4));
    }

    private sealed record ReadAssertionResult(string KeyId, Dictionary<string, string> Values, TimeSpan Lifetime, bool SignatureIsValid)
    {
        public string Claim(string name) => Values[name];
    }
}
