using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class CommerceClientExtensions
{
    public static IServiceCollection AddCommerceClientConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CommerceServiceOptions>()
            .Bind(configuration.GetSection(CommerceServiceOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https", "Commerce base address must be absolute HTTP(S).")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && !string.IsNullOrWhiteSpace(options.Scope)
                && !string.IsNullOrWhiteSpace(options.SigningKeyId), "Commerce assertion claims are incomplete.")
            .Validate(options => ServiceConfigurationExtensions.IsValidPrivateKey(options.SigningKeyBase64),
                "Commerce signing key must be an RSA PKCS#8 key of at least 2048 bits.")
            .ValidateOnStart();
        services.AddTransient(provider => new ServiceAssertionHandler(
            provider.GetRequiredService<ServiceAssertionTokenFactory>(),
            ServiceAssertionDestination.Commerce));
        // The assertion handler is registered after the resilience handler so each attempt signs its own jti.
        var client = services.AddHttpClient<IShowcaseCommerceClient, ShowcaseCommerceClient>((provider, httpClient) =>
        {
            var options = provider.GetRequiredService<IOptions<CommerceServiceOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.BaseAddress);
        });
        client.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.DisableForUnsafeHttpMethods();
        });
        client.AddHttpMessageHandler<ServiceAssertionHandler>();
        return services;
    }
}
