using System.Security.Cryptography;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffAdmin.Api.Extensions;

public static class StaffIdentityExtensions
{
    public static WebApplicationBuilder AddStaffIdentityConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<StaffIdentityOptions>()
            .Bind(builder.Configuration.GetSection(StaffIdentityOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && options.BaseAddress.EndsWith("/", StringComparison.Ordinal),
                "Staff Identity base address must be an absolute HTTP(S) URL ending in a slash.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && !string.IsNullOrWhiteSpace(options.SigningKeyId)
                && IsValidPrivateKey(options.SigningKeyBase64)
                && Guid.TryParse(options.TenantId, out var tenantId)
                && tenantId != Guid.Empty,
                "Staff Identity assertion configuration is invalid.")
            .ValidateOnStart();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<ServiceAssertionTokenFactory>();
        builder.Services.AddHttpClient<IStaffPasswordResetIdentityClient, StaffPasswordResetIdentityClient>(
                (serviceProvider, client) =>
                {
                    var settings = serviceProvider.GetRequiredService<IOptions<StaffIdentityOptions>>().Value;
                    client.BaseAddress = new Uri(settings.BaseAddress, UriKind.Absolute);
                })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        return builder;
    }

    private static bool IsValidPrivateKey(string encodedKey)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(encodedKey), out _);
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
