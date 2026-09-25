using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Api.Extensions;
using CodeForCoders.Identity.Api.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.UnitTests;

public sealed class ServiceAssertionVerifierTests : IDisposable
{
    private const string StudentIssuer = "bff-student";
    private const string AdminIssuer = "bff-admin";
    private const string Audience = "identity-internal";
    private const string StudentKeyId = "student-key";
    private const string AdminKeyId = "admin-key";
    private const string StudentScope = "student-sessions:create";
    private const string StaffScope = "staff-sessions:validate";

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    private readonly RSA _studentKey = RSA.Create(2048);
    private readonly RSA _adminKey = RSA.Create(2048);

    public void Dispose()
    {
        _studentKey.Dispose();
        _adminKey.Dispose();
    }

    [Fact]
    public async Task AcceptsBffAdminAssertionForBackofficeScope()
    {
        var verifier = CreateVerifier();
        var assertion = CreateAssertion(AdminIssuer, _adminKey, AdminKeyId, StaffScope);

        var verified = await verifier.VerifyAsync(assertion, StaffScope, TestContext.Current.CancellationToken);

        Assert.NotNull(verified);
        Assert.Equal(TenantId, verified.TenantId);
    }

    [Fact]
    public async Task AcceptsBffStudentAssertionForStudentScope()
    {
        var verifier = CreateVerifier();
        var assertion = CreateAssertion(StudentIssuer, _studentKey, StudentKeyId, StudentScope);

        var verified = await verifier.VerifyAsync(assertion, StudentScope, TestContext.Current.CancellationToken);

        Assert.NotNull(verified);
        Assert.Equal(TenantId, verified.TenantId);
    }

    [Fact]
    public async Task RejectsBffStudentAssertionForBackofficeScope()
    {
        var verifier = CreateVerifier();
        var assertion = CreateAssertion(StudentIssuer, _studentKey, StudentKeyId, StaffScope);

        var verified = await verifier.VerifyAsync(assertion, StaffScope, TestContext.Current.CancellationToken);

        Assert.Null(verified);
    }

    [Fact]
    public async Task RejectsBffAdminAssertionForStudentScope()
    {
        var verifier = CreateVerifier();
        var assertion = CreateAssertion(AdminIssuer, _adminKey, AdminKeyId, StudentScope);

        var verified = await verifier.VerifyAsync(assertion, StudentScope, TestContext.Current.CancellationToken);

        Assert.Null(verified);
    }

    [Fact]
    public async Task RejectsUnknownIssuer()
    {
        var verifier = CreateVerifier();
        using var unknownKey = RSA.Create(2048);
        var assertion = CreateAssertion("bff-unknown", unknownKey, "unknown-key", StaffScope);

        var verified = await verifier.VerifyAsync(assertion, StaffScope, TestContext.Current.CancellationToken);

        Assert.Null(verified);
    }

    [Fact]
    public async Task RejectsAssertionSignedWithAnotherIssuerKey()
    {
        var verifier = CreateVerifier();
        var assertion = CreateAssertion(StudentIssuer, _adminKey, AdminKeyId, StudentScope);

        var verified = await verifier.VerifyAsync(assertion, StudentScope, TestContext.Current.CancellationToken);

        Assert.Null(verified);
    }

    private ServiceAssertionVerifier CreateVerifier()
    {
        var options = new ServiceAssertionOptions
        {
            Audience = Audience,
            Issuers = new Dictionary<string, ServiceAssertionIssuerOptions>(StringComparer.Ordinal)
            {
                [StudentIssuer] = new ServiceAssertionIssuerOptions
                {
                    PublicKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [StudentKeyId] = Convert.ToBase64String(_studentKey.ExportSubjectPublicKeyInfo()),
                    },
                    AllowedScopes = [.. ServiceAssertionScopes.Student],
                    AllowedTenantIds = [TenantId.ToString("D")],
                },
                [AdminIssuer] = new ServiceAssertionIssuerOptions
                {
                    PublicKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [AdminKeyId] = Convert.ToBase64String(_adminKey.ExportSubjectPublicKeyInfo()),
                    },
                    AllowedScopes = [.. ServiceAssertionScopes.Staff],
                    AllowedTenantIds = [TenantId.ToString("D")],
                },
            },
        };

        Assert.True(ServiceAssertionConfigurationValidator.TryValidate(options, out _));
        return new ServiceAssertionVerifier(
            Options.Create(options),
            new AcceptingReplayStore(),
            new RecordingTenantContext(),
            TimeProvider.System);
    }

    private static string CreateAssertion(string issuer, RSA signingKey, string keyId, string scope)
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT",
            kid = keyId,
        }));
        var claims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = issuer,
            aud = Audience,
            sub = issuer,
            tenantId = TenantId.ToString("D"),
            scope,
            jti = Guid.CreateVersion7(now).ToString("D"),
            iat = now.ToUnixTimeSeconds(),
            nbf = now.AddSeconds(-5).ToUnixTimeSeconds(),
            exp = now.AddSeconds(30).ToUnixTimeSeconds(),
        }));
        var signature = signingKey.SignData(
            Encoding.ASCII.GetBytes($"{header}.{claims}"),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class AcceptingReplayStore : Application.Interfaces.IServiceAssertionReplayStore
    {
        public Task<bool> TryConsumeAsync(Guid assertionId, DateTimeOffset expiresOn, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class RecordingTenantContext : Application.Common.ITenantContext
    {
        public Guid? TenantId { get; private set; }

        public void Set(Guid tenantId)
        {
            TenantId = tenantId;
        }
    }
}
