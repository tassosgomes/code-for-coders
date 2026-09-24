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
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience), "Service assertion issuer and audience are required.")
            .Validate(options => options.PublicKeys.Count > 0 && options.PublicKeys.All(IsValidPublicKey),
                "At least one valid service assertion public key is required.")
            .Validate(options => options.AllowedTenantIds.Length > 0
                && options.AllowedTenantIds.All(value => Guid.TryParse(value, out var tenantId) && tenantId != Guid.Empty),
                "At least one allowed tenant id is required for service assertions.")
            .ValidateOnStart();
        services.AddScoped<ServiceAssertionVerifier>();
        return services;
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
