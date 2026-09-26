using System.Security.Cryptography;
using CodeForCoders.Identity.Api.Security;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Api.Extensions;

public static class ServiceAssertionExtensions
{
    public static IServiceCollection AddServiceAssertionConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServiceAssertionOptions>()
            .Bind(configuration.GetSection(ServiceAssertionOptions.SectionName))
            .Validate(options => ServiceAssertionConfigurationValidator.TryValidate(options, out _),
                "Invalid service assertion configuration: each issuer requires a name, at least one valid public key (RSA 2048+), at least one allowed scope and at least one allowed tenant id.")
            .ValidateOnStart();
        services.AddScoped<ServiceAssertionVerifier>();
        return services;
    }
}

public static class ServiceAssertionConfigurationValidator
{
    public static bool TryValidate(ServiceAssertionOptions options, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            error = "Service assertion audience is required.";
            return false;
        }

        var issuers = options.GetEffectiveIssuers();
        if (issuers.Count == 0)
        {
            error = "At least one service assertion issuer is required.";
            return false;
        }

        foreach (var issuer in issuers)
        {
            if (!TryValidateIssuer(issuer.Key, issuer.Value, out error))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryValidateIssuer(string name, ServiceAssertionIssuerOptions issuer, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(name) || issuer is null)
        {
            error = "Service assertion issuer name is required.";
            return false;
        }

        if (issuer.PublicKeys.Count == 0)
        {
            error = $"Service assertion issuer '{name}' requires at least one public key.";
            return false;
        }

        if (!issuer.PublicKeys.All(IsValidPublicKey))
        {
            error = $"Service assertion issuer '{name}' has an invalid public key (RSA 2048+ required).";
            return false;
        }

        if (issuer.AllowedScopes.Length == 0 || issuer.AllowedScopes.Any(string.IsNullOrWhiteSpace))
        {
            error = $"Service assertion issuer '{name}' requires at least one allowed scope.";
            return false;
        }

        if (issuer.AllowedTenantIds.Length == 0
            || issuer.AllowedTenantIds.Any(value => !Guid.TryParse(value, out var tenantId) || tenantId == Guid.Empty))
        {
            error = $"Service assertion issuer '{name}' requires at least one allowed tenant id.";
            return false;
        }

        return true;
    }

    private static bool IsValidPublicKey(KeyValuePair<string, string> key)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key.Value), out _);
            return key.Key.Length > 0 && rsa.KeySize >= 2048;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
