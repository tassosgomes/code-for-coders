using System.Security.Cryptography;
using CodeForCoders.Billing.Api.Security;
using CodeForCoders.Billing.Application;
using CodeForCoders.Billing.Infra.Data;
using CodeForCoders.Billing.Infra.Messaging;

namespace CodeForCoders.Billing.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    private static readonly string[] DevelopmentEnvironments =
        ["Development", "EndToEndTest", "IntegrationTest", "Test"];

    public static WebApplicationBuilder AddBillingConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddBillingConfigurationValidation(builder.Environment);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        return builder;
    }

    private static IServiceCollection AddBillingConfigurationValidation(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        var development = DevelopmentEnvironments.Contains(
            environment.EnvironmentName,
            StringComparer.OrdinalIgnoreCase);

        services.AddOptions<StripeOptions>()
            .BindConfiguration(StripeOptions.SectionName)
            .Validate(options => development || !string.IsNullOrWhiteSpace(options.SecretKey),
                "Stripe:SecretKey is required outside Development and test environments.")
            .Validate(options => development || !string.IsNullOrWhiteSpace(options.WebhookSigningSecret),
                "Stripe:WebhookSigningSecret is required outside Development and test environments.")
            .ValidateOnStart();
        services.AddOptions<PaymentReturnOptions>()
            .BindConfiguration(PaymentReturnOptions.SectionName)
            .Validate(options => development || options.AllowedHosts.Length > 0,
                "PaymentReturn:AllowedHosts must list at least one host outside Development and test environments.")
            .Validate(options => options.AllowedHosts.All(host => !string.IsNullOrWhiteSpace(host)),
                "PaymentReturn:AllowedHosts must not contain empty hosts.")
            .ValidateOnStart();
        services.AddOptions<ServiceAssertionOptions>()
            .BindConfiguration(ServiceAssertionOptions.SectionName)
            .Validate(options => options.Audience == "billing",
                "Service assertion audience must be billing.")
            .Validate(options => options.Issuers.All(IsValidIssuer),
                "Service assertion issuers are incomplete or invalid.")
            .ValidateOnStart();
        return services;
    }

    private static bool IsValidIssuer(KeyValuePair<string, ServiceAssertionIssuerOptions> entry)
    {
        var issuer = entry.Value;
        return issuer.PublicKeys.Count > 0
            && issuer.PublicKeys.Values.All(IsValidPublicKey)
            && issuer.AllowedScopes.Length > 0
            && issuer.AllowedScopes.All(scope => scope == "payment:request")
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
