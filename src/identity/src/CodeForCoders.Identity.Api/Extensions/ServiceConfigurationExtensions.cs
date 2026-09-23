using CodeForCoders.Identity.Application;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Messaging;
using CodeForCoders.Identity.Api.Security;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddIdentityConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddServiceAssertionConfiguration(builder.Configuration);
        builder.Services.AddOptions<StudentSessionTokenOptions>()
            .Bind(builder.Configuration.GetSection(StudentSessionTokenOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer)
                && options.LifetimeMinutes is >= 1 and <= 15,
                "Student session token issuer and lifetime are invalid.")
            .Validate(options => options.AudienceScopes.Count == 0
                || (!string.IsNullOrWhiteSpace(options.SigningKeyId)
                    && IsValidPrivateKey(options.SigningKeyBase64)
                    && options.AudienceScopes.All(pair => !string.IsNullOrWhiteSpace(pair.Key)
                        && !string.IsNullOrWhiteSpace(pair.Value))),
                "Configured student session audiences require an RSA signing key and a non-empty scope.")
            .ValidateOnStart();
        builder.Services.AddSingleton<StudentSessionTokenIssuer>();
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddHttpClient("identity-outbound")
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
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
