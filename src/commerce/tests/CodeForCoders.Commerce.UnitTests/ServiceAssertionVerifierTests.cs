using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

[Trait("Api", "ServiceAssertionVerifier - Unit")]
public sealed class ServiceAssertionVerifierTests : IDisposable
{
    private const string Issuer = "bff-student";
    private const string KeyId = "local-commerce-1";
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly RSA bffKey = RSA.Create(2048);
    private readonly RSA otherKey = RSA.Create(2048);
    private readonly InMemoryReplayStore replayStore = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        bffKey.Dispose();
        otherKey.Dispose();
    }

    [Fact(DisplayName = nameof(AssertionSignedByTheRealBffFactoryIsAccepted))]
    public async Task AssertionSignedByTheRealBffFactoryIsAccepted()
    {
        var token = BffFactory().Create(ServiceAssertionDestination.Commerce, "showcase:read");

        var assertion = await Verifier().VerifyAsync(token, Cancellation);

        Assert.NotNull(assertion);
        Assert.Equal(Issuer, assertion.Issuer);
        Assert.Equal(Tenant, assertion.TenantId);
        Assert.Equal(["showcase:read"], assertion.GrantedScopes);
        Assert.True(assertion.ExpiresOn > Now);
    }

    [Fact(DisplayName = nameof(IssuerAllowListLimitsTheGrantedScopes))]
    public async Task IssuerAllowListLimitsTheGrantedScopes()
    {
        var token = BffFactory().Create(ServiceAssertionDestination.Commerce, "showcase:read");

        var assertion = await Verifier(allowedScopes: ["purchase-intent:write"]).VerifyAsync(token, Cancellation);

        Assert.NotNull(assertion);
        Assert.Empty(assertion.GrantedScopes);
    }

    [Fact(DisplayName = nameof(UnknownKeyIdIsRejected))]
    public async Task UnknownKeyIdIsRejected()
    {
        var token = Sign(Claims(), keyId: "never-registered");

        Assert.Null(await Verifier().VerifyAsync(token, Cancellation));
    }

    [Fact(DisplayName = nameof(SignatureFromAnotherKeyIsRejectedWithoutConsumingTheIdentifier))]
    public async Task SignatureFromAnotherKeyIsRejectedWithoutConsumingTheIdentifier()
    {
        var token = Sign(Claims(), signer: otherKey);

        Assert.Null(await Verifier().VerifyAsync(token, Cancellation));
        Assert.Equal(0, replayStore.Count);
    }

    [Fact(DisplayName = nameof(AssertionForIdentityIsNotAcceptedByCommerce))]
    public async Task AssertionForIdentityIsNotAcceptedByCommerce()
    {
        var token = BffFactory().Create(ServiceAssertionDestination.Identity, "student-sessions:validate");

        Assert.Null(await Verifier().VerifyAsync(token, Cancellation));
    }

    [Fact(DisplayName = nameof(WrongAudienceIsRejected))]
    public async Task WrongAudienceIsRejected()
        => Assert.Null(await Verifier().VerifyAsync(Sign(Claims(audience: "identity-internal")), Cancellation));

    [Fact(DisplayName = nameof(ExpiredAssertionIsRejected))]
    public async Task ExpiredAssertionIsRejected()
        => Assert.Null(await Verifier().VerifyAsync(
            Sign(Claims(issuedAt: Now.AddMinutes(-5), notBefore: Now.AddMinutes(-5), expiresOn: Now.AddMinutes(-4))), Cancellation));

    [Fact(DisplayName = nameof(LifetimeAboveSixtySecondsIsRejected))]
    public async Task LifetimeAboveSixtySecondsIsRejected()
        => Assert.Null(await Verifier().VerifyAsync(Sign(Claims(expiresOn: Now.AddSeconds(61))), Cancellation));

    [Fact(DisplayName = nameof(NotBeforeBeyondTheClockSkewIsRejected))]
    public async Task NotBeforeBeyondTheClockSkewIsRejected()
        => Assert.Null(await Verifier().VerifyAsync(Sign(Claims(notBefore: Now.AddMinutes(5))), Cancellation));

    [Fact(DisplayName = nameof(TenantOutsideTheIssuerAllowListIsRejected))]
    public async Task TenantOutsideTheIssuerAllowListIsRejected()
        => Assert.Null(await Verifier().VerifyAsync(Sign(Claims(tenant: Guid.CreateVersion7())), Cancellation));

    [Fact(DisplayName = nameof(SubjectDifferentFromIssuerIsRejected))]
    public async Task SubjectDifferentFromIssuerIsRejected()
        => Assert.Null(await Verifier().VerifyAsync(Sign(Claims(subject: "someone-else")), Cancellation));

    [Fact(DisplayName = nameof(UnknownIssuerIsRejected))]
    public async Task UnknownIssuerIsRejected()
        => Assert.Null(await Verifier().VerifyAsync(Sign(Claims(issuer: "bff-admin", subject: "bff-admin")), Cancellation));

    [Fact(DisplayName = nameof(AssertionIdentifierCanOnlyBeConsumedOnce))]
    public async Task AssertionIdentifierCanOnlyBeConsumedOnce()
    {
        var token = BffFactory().Create(ServiceAssertionDestination.Commerce, "showcase:read");
        var verifier = Verifier();

        Assert.NotNull(await verifier.VerifyAsync(token, Cancellation));
        Assert.Null(await verifier.VerifyAsync(token, Cancellation));
    }

    [Fact(DisplayName = nameof(OldAndNewKeysAreBothAcceptedDuringRotation))]
    public async Task OldAndNewKeysAreBothAcceptedDuringRotation()
    {
        var verifier = Verifier(extraKeys: new() { ["local-commerce-2"] = Convert.ToBase64String(otherKey.ExportSubjectPublicKeyInfo()) });

        Assert.NotNull(await verifier.VerifyAsync(Sign(Claims()), Cancellation));
        Assert.NotNull(await verifier.VerifyAsync(Sign(Claims(), signer: otherKey, keyId: "local-commerce-2"), Cancellation));
    }

    [Fact(DisplayName = nameof(UnsignedOrNonRs256TokensAreRejected))]
    public async Task UnsignedOrNonRs256TokensAreRejected()
    {
        var verifier = Verifier();

        Assert.Null(await verifier.VerifyAsync(Sign(Claims(), algorithm: "none"), Cancellation));
        Assert.Null(await verifier.VerifyAsync(Sign(Claims(), algorithm: "HS256"), Cancellation));
    }

    [Theory(DisplayName = nameof(MalformedTokensAreRejected))]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    [InlineData("e30.e30.e30")]
    public async Task MalformedTokensAreRejected(string? token)
        => Assert.Null(await Verifier().VerifyAsync(token, Cancellation));

    private ServiceAssertionVerifier Verifier(string[]? allowedScopes = null, Dictionary<string, string>? extraKeys = null)
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [KeyId] = Convert.ToBase64String(bffKey.ExportSubjectPublicKeyInfo()),
        };
        foreach (var (id, key) in extraKeys ?? [])
        {
            keys[id] = key;
        }

        var options = new ServiceAssertionOptions
        {
            Audience = "commerce",
            Issuers =
            {
                [Issuer] = new ServiceAssertionIssuerOptions
                {
                    PublicKeys = keys,
                    AllowedScopes = allowedScopes ?? ["showcase:read", "purchase-intent:write"],
                    AllowedTenantIds = [Tenant.ToString("D")],
                },
            },
        };
        return new ServiceAssertionVerifier(Options.Create(options), replayStore, new FixedTimeProvider(Now));
    }

    private ServiceAssertionTokenFactory BffFactory()
        => new(
            Options.Create(new StudentIdentityOptions
            {
                SigningKeyId = "identity-key",
                SigningKeyBase64 = Convert.ToBase64String(otherKey.ExportPkcs8PrivateKey()),
                TenantId = Tenant.ToString("D"),
            }),
            Options.Create(new CommerceServiceOptions
            {
                SigningKeyId = KeyId,
                SigningKeyBase64 = Convert.ToBase64String(bffKey.ExportPkcs8PrivateKey()),
            }),
            new FixedTimeProvider(Now));

    private static Dictionary<string, object> Claims(
        string issuer = Issuer,
        string subject = Issuer,
        string audience = "commerce",
        Guid? tenant = null,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expiresOn = null)
        => new()
        {
            ["iss"] = issuer,
            ["sub"] = subject,
            ["aud"] = audience,
            ["tenantId"] = (tenant ?? Tenant).ToString("D"),
            ["scope"] = "showcase:read",
            ["jti"] = Guid.CreateVersion7().ToString("D"),
            ["iat"] = (issuedAt ?? Now).ToUnixTimeSeconds(),
            ["nbf"] = (notBefore ?? Now.AddSeconds(-5)).ToUnixTimeSeconds(),
            ["exp"] = (expiresOn ?? Now.AddSeconds(30)).ToUnixTimeSeconds(),
        };

    private string Sign(Dictionary<string, object> claims, RSA? signer = null, string keyId = KeyId, string algorithm = "RS256")
    {
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["alg"] = algorithm, ["typ"] = "JWT", ["kid"] = keyId }));
        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(claims));
        var signature = (signer ?? bffKey).SignData(Encoding.ASCII.GetBytes($"{header}.{body}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{body}.{Encode(signature)}";
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryReplayStore : IServiceAssertionReplayStore
    {
        private readonly HashSet<Guid> consumed = [];

        public int Count => consumed.Count;

        public Task<bool> TryConsumeAsync(Guid assertionId, DateTimeOffset expiresOn, CancellationToken cancellationToken)
            => Task.FromResult(consumed.Add(assertionId));
    }
}
