using Microsoft.Extensions.Http.Resilience;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Infra.Gateway.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace CodeForCoders.Billing.Infra.Gateway;

public static class DependencyInjection
{
    public static IServiceCollection AddGatewayConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StripeGatewayOptions>().Bind(configuration.GetSection("Stripe"))
         .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http", "Gateway URL is invalid.").ValidateOnStart();
        services.AddHttpClient<IPaymentGateway, StripeGatewayAdapter>((provider, client) =>
          client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<StripeGatewayOptions>>().Value.BaseUrl))
         .AddStandardResilienceHandler(x =>
         {
             x.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
             x.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20); x.Retry.MaxRetryAttempts = 3;
             x.Retry.DisableForUnsafeHttpMethods();
         });
        return services;
    }
}
