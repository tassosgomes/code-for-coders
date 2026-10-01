using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests.Clients;

public sealed class ServiceAssertionTokenFactoryTests : IDisposable
{
    private const string TenantId = "00000000-0000-7000-8000-000000000001";

    private readonly RSA identityKey = RSA.Create(2048);
    private readonly RSA commerceKey = RSA.Create(2048);

    public void Dispose()
    {
        identityKey.Dispose();
        commerceKey.Dispose();
    }

    [Fact(DisplayName = nameof(CommerceAssertionTargetsCommerceWithItsOwnScopeKeyAndTenant))]
    public void CommerceAssertionTargetsCommerceWithItsOwnScopeKeyAndTenant()
    {
        var token = CreateFactory().Create(ServiceAssertionDestination.Commerce, "showcase:read");

        var (header, claims) = Decode(token);
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal("commerce-key", header.GetProperty("kid").GetString());
        Assert.Equal("bff-student", claims.GetProperty("iss").GetString());
        Assert.Equal("bff-student", claims.GetProperty("sub").GetString());
        Assert.Equal("commerce", claims.GetProperty("aud").GetString());
        Assert.Equal("showcase:read", claims.GetProperty("scope").GetString());
        Assert.Equal(TenantId, claims.GetProperty("tenantId").GetString());
        Assert.InRange(claims.GetProperty("exp").GetInt64() - claims.GetProperty("iat").GetInt64(), 1, 60);
    }

    [Fact(DisplayName = nameof(CommerceAssertionIsSignedWithTheDedicatedKeyOnly))]
    public void CommerceAssertionIsSignedWithTheDedicatedKeyOnly()
    {
        var token = CreateFactory().Create(ServiceAssertionDestination.Commerce, "showcase:read");

        Assert.True(IsSignedBy(token, commerceKey));
        Assert.False(IsSignedBy(token, identityKey));
    }

    [Fact(DisplayName = nameof(IdentityAssertionKeepsItsAudienceAndKey))]
    public void IdentityAssertionKeepsItsAudienceAndKey()
    {
        var token = CreateFactory().Create("student-sessions:validate");

        var (header, claims) = Decode(token);
        Assert.Equal("identity-key", header.GetProperty("kid").GetString());
        Assert.Equal("identity-internal", claims.GetProperty("aud").GetString());
        Assert.True(IsSignedBy(token, identityKey));
    }

    [Fact(DisplayName = nameof(EveryAssertionCarriesAUniqueIdentifier))]
    public void EveryAssertionCarriesAUniqueIdentifier()
    {
        var factory = CreateFactory();

        var identifiers = Enumerable.Range(0, 20)
            .Select(_ => Decode(factory.Create(ServiceAssertionDestination.Commerce, "showcase:read")).Claims.GetProperty("jti").GetString())
            .ToArray();

        Assert.Equal(20, identifiers.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact(DisplayName = nameof(ScopeNotConfiguredForTheDestinationIsRefused))]
    public void ScopeNotConfiguredForTheDestinationIsRefused()
    {
        var factory = CreateFactory();

        Assert.Throws<InvalidOperationException>(() => factory.Create(ServiceAssertionDestination.Commerce, "student-sessions:validate"));
        Assert.Throws<InvalidOperationException>(() => factory.Create(ServiceAssertionDestination.Identity, "showcase:read"));
    }

    private ServiceAssertionTokenFactory CreateFactory()
        => new(
            Options.Create(new StudentIdentityOptions
            {
                Scope = "student-sessions:validate",
                SigningKeyId = "identity-key",
                SigningKeyBase64 = Convert.ToBase64String(identityKey.ExportPkcs8PrivateKey()),
                TenantId = TenantId,
            }),
            Options.Create(new CommerceServiceOptions
            {
                SigningKeyId = "commerce-key",
                SigningKeyBase64 = Convert.ToBase64String(commerceKey.ExportPkcs8PrivateKey()),
            }),
            TimeProvider.System);

    private static (JsonElement Header, JsonElement Claims) Decode(string token)
    {
        var segments = token.Split('.');
        return (JsonDocument.Parse(DecodeSegment(segments[0])).RootElement, JsonDocument.Parse(DecodeSegment(segments[1])).RootElement);
    }

    private static bool IsSignedBy(string token, RSA key)
    {
        var segments = token.Split('.');
        return key.VerifyData(
            Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}"),
            DecodeSegment(segments[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    private static byte[] DecodeSegment(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - (padded.Length % 4)) % 4));
    }
}
