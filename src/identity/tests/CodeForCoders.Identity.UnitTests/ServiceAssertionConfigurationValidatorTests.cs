using System.Security.Cryptography;
using CodeForCoders.Identity.Api.Extensions;
using CodeForCoders.Identity.Api.Security;
using Xunit;

namespace CodeForCoders.Identity.UnitTests;

public sealed class ServiceAssertionConfigurationValidatorTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-7000-8000-000000000001");

    private readonly RSA _studentKey = RSA.Create(2048);
    private readonly RSA _adminKey = RSA.Create(2048);
    private readonly RSA _weakKey = RSA.Create(1024);

    public void Dispose()
    {
        _studentKey.Dispose();
        _adminKey.Dispose();
        _weakKey.Dispose();
    }

    [Fact]
    public void AcceptsTwoIssuersWithKeysScopesAndTenants()
    {
        var options = ValidOptions();

        Assert.True(ServiceAssertionConfigurationValidator.TryValidate(options, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void RejectsIssuerWithoutKey()
    {
        var options = ValidOptions();
        options.Issuers["bff-admin"].PublicKeys.Clear();

        Assert.False(ServiceAssertionConfigurationValidator.TryValidate(options, out var error));
        Assert.Contains("bff-admin", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsKeySmallerThan2048Bits()
    {
        var options = ValidOptions();
        options.Issuers["bff-admin"].PublicKeys["weak-key"] =
            Convert.ToBase64String(_weakKey.ExportSubjectPublicKeyInfo());
        options.Issuers["bff-admin"].PublicKeys.Remove("admin-key");

        Assert.False(ServiceAssertionConfigurationValidator.TryValidate(options, out _));
    }

    [Fact]
    public void RejectsIssuerWithoutScope()
    {
        var options = ValidOptions();
        options.Issuers["bff-admin"].AllowedScopes = [];

        Assert.False(ServiceAssertionConfigurationValidator.TryValidate(options, out var error));
        Assert.Contains("bff-admin", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidTenant()
    {
        var options = ValidOptions();
        options.Issuers["bff-admin"].AllowedTenantIds = ["not-a-tenant"];

        Assert.False(ServiceAssertionConfigurationValidator.TryValidate(options, out _));
    }

    [Fact]
    public void RejectsMissingAudience()
    {
        var options = ValidOptions();
        options.Audience = string.Empty;

        Assert.False(ServiceAssertionConfigurationValidator.TryValidate(options, out _));
    }

    [Fact]
    public void MapsLegacySingleIssuerConfigurationToBffStudent()
    {
        var options = new ServiceAssertionOptions
        {
            Audience = "identity-internal",
            Issuer = "bff-student",
            PublicKeys = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["legacy-key"] = Convert.ToBase64String(_studentKey.ExportSubjectPublicKeyInfo()),
            },
            AllowedTenantIds = [TenantId.ToString("D")],
        };

        Assert.True(ServiceAssertionConfigurationValidator.TryValidate(options, out _));
        var effective = options.GetEffectiveIssuers();
        var issuer = Assert.Single(effective);
        Assert.Equal("bff-student", issuer.Key);
        Assert.Equal(ServiceAssertionScopes.Student, issuer.Value.AllowedScopes);
    }

    private ServiceAssertionOptions ValidOptions()
        => new()
        {
            Audience = "identity-internal",
            Issuers = new Dictionary<string, ServiceAssertionIssuerOptions>(StringComparer.Ordinal)
            {
                ["bff-student"] = new ServiceAssertionIssuerOptions
                {
                    PublicKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["student-key"] = Convert.ToBase64String(_studentKey.ExportSubjectPublicKeyInfo()),
                    },
                    AllowedScopes = [.. ServiceAssertionScopes.Student],
                    AllowedTenantIds = [TenantId.ToString("D")],
                },
                ["bff-admin"] = new ServiceAssertionIssuerOptions
                {
                    PublicKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["admin-key"] = Convert.ToBase64String(_adminKey.ExportSubjectPublicKeyInfo()),
                    },
                    AllowedScopes = [.. ServiceAssertionScopes.Staff],
                    AllowedTenantIds = [TenantId.ToString("D")],
                },
            },
        };
}
