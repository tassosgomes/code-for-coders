using CodeForCoders.Commerce.Api.Clients;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
namespace CodeForCoders.Commerce.Api.Extensions;

public static class BillingClientExtensions
{
    public static IServiceCollection AddBillingClientConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StudentAppOptions>().Bind(configuration.GetSection("StudentApp"))
         .Validate(x => Uri.TryCreate(x.PublicBaseUrl, UriKind.Absolute, out var url) && url.Scheme is "https" or "http", "Student app URL is invalid.").ValidateOnStart();
        services.AddOptions<BillingClientOptions>().Bind(configuration.GetSection("BillingClient"))
         .Validate(x => x.Issuer == "commerce" && x.Audience == "billing" && Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _), "Billing client settings are invalid.").ValidateOnStart();
        services.AddScoped<BillingAssertionTokenFactory>();
        services.AddHttpClient<IBillingPaymentClient, BillingPaymentClient>((provider, client) =>
         client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<BillingClientOptions>>().Value.BaseUrl))
         .AddStandardResilienceHandler(x =>
         {
             x.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
             x.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20); x.Retry.MaxRetryAttempts = 3; x.Retry.DisableForUnsafeHttpMethods();
         });
        return services;
    }
}
