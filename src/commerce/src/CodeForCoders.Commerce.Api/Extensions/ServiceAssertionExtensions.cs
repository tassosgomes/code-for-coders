using System.Security.Cryptography;
using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Api.Security;
using Microsoft.AspNetCore.Authentication;

namespace CodeForCoders.Commerce.Api.Extensions;

public static class ServiceAssertionExtensions
{
    public static IServiceCollection AddServiceAssertionConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ServiceAssertionOptions>()
            .Bind(configuration.GetSection(ServiceAssertionOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Service assertion audience is required.")
            .Validate(options => options.Issuers.All(IsValidIssuer), "Service assertion issuers are incomplete or invalid.")
            .ValidateOnStart();
        services.AddScoped<ServiceAssertionVerifier>();
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ServiceAssertionAuthenticationHandler>(
                ServiceAssertionAuthenticationHandler.SchemeName,
                _ => { });
        services.AddAuthorization(options => options.AddPolicy(
            ShowcasePolicies.Read,
            policy => policy.AddAuthenticationSchemes(ServiceAssertionAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ServiceAssertionAuthenticationHandler.ScopeClaim, ServiceAssertionScopes.ShowcaseRead)));
        services.AddAuthorization(options => options.AddPolicy(ShowcasePolicies.PurchaseIntent,
            policy => policy.AddAuthenticationSchemes(ServiceAssertionAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ServiceAssertionAuthenticationHandler.ScopeClaim, ServiceAssertionScopes.PurchaseIntentWrite)));
        services.AddAuthorization(options => options.AddPolicy(AccessDecisionPolicies.Read,
            policy => policy.AddAuthenticationSchemes(ServiceAssertionAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ServiceAssertionAuthenticationHandler.ScopeClaim, ServiceAssertionScopes.AccessDecisionRead)
                .RequireAssertion(context => context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value != "bff-student")));
        services.AddAuthorization(options => options.AddPolicy(StudentCourseAccessPolicies.Read,
            policy => policy.AddAuthenticationSchemes(ServiceAssertionAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ServiceAssertionAuthenticationHandler.ScopeClaim, ServiceAssertionScopes.CourseAccessRead)
                .RequireClaim(System.Security.Claims.ClaimTypes.NameIdentifier, "learning")));
        return services;
    }

    private static bool IsValidIssuer(KeyValuePair<string, ServiceAssertionIssuerOptions> entry)
    {
        var issuer = entry.Value;
        var scopes = entry.Key == "bff-student" ? ServiceAssertionScopes.Student : ServiceAssertionScopes.All;
        return issuer.PublicKeys.Count > 0
            && issuer.PublicKeys.Values.All(IsValidPublicKey)
            && issuer.AllowedScopes.Length > 0
            && issuer.AllowedScopes.All(scope => scopes.Contains(scope, StringComparer.Ordinal))
            && issuer.AllowedTenantIds.Length > 0
            && issuer.AllowedTenantIds.All(value => Guid.TryParse(value, out var tenant) && tenant != Guid.Empty);
    }

    private static bool IsValidPublicKey(string encodedKey)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(encodedKey), out _);
            return rsa.KeySize >= 2048;
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
