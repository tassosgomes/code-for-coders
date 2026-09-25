using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Identity.Api.Endpoints;
using CodeForCoders.Identity.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

public sealed class UserTokenSigningKeysTests
{
    [Fact(DisplayName = nameof(UserTokenSigningKeys_ReturnsCurrentPublicKeyWithoutAuthentication))]
    [Trait("Layer", "Identity user token signing keys - Integration")]
    public async Task UserTokenSigningKeys_ReturnsCurrentPublicKeyWithoutAuthentication()
    {
        using var currentKey = RSA.Create(2048);
        await using var server = await CreateServer(CreateOptions(currentKey));
        using var client = server.GetTestClient();

        using var response = await client.GetAsync("/internal/v1/jwks", TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var key = document.RootElement.GetProperty("keys").EnumerateArray().Single();
        var publicParameters = currentKey.ExportParameters(includePrivateParameters: false);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("current-key", key.GetProperty("kid").GetString());
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.Equal(Base64UrlEncode(publicParameters.Modulus!), key.GetProperty("n").GetString());
        Assert.Equal(Base64UrlEncode(publicParameters.Exponent!), key.GetProperty("e").GetString());
    }

    [Fact(DisplayName = nameof(UserTokenSigningKeys_IncludesPreviousPublicKeyDuringRotation))]
    [Trait("Layer", "Identity user token signing keys - Integration")]
    public async Task UserTokenSigningKeys_IncludesPreviousPublicKeyDuringRotation()
    {
        using var currentKey = RSA.Create(2048);
        using var previousKey = RSA.Create(2048);
        await using var server = await CreateServer(CreateOptions(currentKey, previousKey));
        using var client = server.GetTestClient();

        using var response = await client.GetAsync("/internal/v1/jwks", TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var keys = document.RootElement.GetProperty("keys").EnumerateArray().ToArray();

        Assert.Equal(2, keys.Length);
        Assert.Contains(keys, key => key.GetProperty("kid").GetString() == "current-key");
        Assert.Contains(keys, key => key.GetProperty("kid").GetString() == "previous-key");
    }

    [Fact(DisplayName = nameof(UserTokenSigningKeys_NeverSerializesPrivateKeyMaterial))]
    [Trait("Layer", "Identity user token signing keys - Integration")]
    public async Task UserTokenSigningKeys_NeverSerializesPrivateKeyMaterial()
    {
        using var currentKey = RSA.Create(2048);
        await using var server = await CreateServer(CreateOptions(currentKey));
        using var client = server.GetTestClient();

        using var response = await client.GetAsync("/internal/v1/jwks", TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var key = document.RootElement.GetProperty("keys").EnumerateArray().Single();

        Assert.Equal(
            new[] { "alg", "e", "kid", "kty", "n", "use" },
            key.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain("d", key.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("p", key.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("q", key.EnumerateObject().Select(property => property.Name));
    }

    private static async Task<WebApplication> CreateServer(StaffSessionTokenOptions options)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IOptions<StaffSessionTokenOptions>>(Options.Create(options));
        builder.Services.AddSingleton<UserTokenSigningKeySet>();
        var app = builder.Build();
        app.MapSigningKeyEndpoints();
        await app.StartAsync();
        return app;
    }

    private static StaffSessionTokenOptions CreateOptions(RSA currentKey, RSA? previousKey = null)
    {
        var options = new StaffSessionTokenOptions
        {
            Issuer = "identity",
            SigningKeyId = "current-key",
            SigningKeyBase64 = Convert.ToBase64String(currentKey.ExportPkcs8PrivateKey()),
            AudienceScopes = new Dictionary<string, string> { ["commerce"] = "finance-area:read" },
        };
        if (previousKey is not null)
        {
            options.PreviousSigningPublicKeys["previous-key"] = Convert.ToBase64String(
                previousKey.ExportSubjectPublicKeyInfo());
        }

        return options;
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
