using CodeForCoders.Learning.Api.Clients;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Learning.Api.Extensions;

public static class AccessDecisionConfigurationExtensions
{
    public static IServiceCollection AddAccessDecisionConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AccessDecisionOptions>().Bind(configuration.GetSection(AccessDecisionOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                && options.TimeoutSeconds is >= 1 and <= 2 && options.CacheSeconds is >= 1 and <= 30,
                "Access decision address and limits are invalid.")
            .Validate(options => string.IsNullOrEmpty(options.SigningKeyId) && string.IsNullOrEmpty(options.SigningKeyBase64)
                || options.SigningKeyId.Length > 0 && IsValidKey(options.SigningKeyBase64), "Access decision signing key is invalid.")
            .ValidateOnStart();
        services.AddMemoryCache();
        services.AddSingleton<AccessDecisionAssertionFactory>();
        // A single bounded attempt implements D-03. No resilience handler retries this decision.
        services.AddHttpClient<IAccessDecisionClient, AccessDecisionClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<AccessDecisionOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        // The course list must always reflect current access; no cache or retries (ADR-0015).
        services.AddHttpClient<IStudentCourseAccessClient, StudentCourseAccessClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<AccessDecisionOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        return services;
    }

    private static bool IsValidKey(string value)
    {
        try
        {
            using var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(value), out _);
            return rsa.KeySize >= 2048;
        }
        catch (FormatException) { return false; }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }
}
