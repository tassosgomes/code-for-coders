using CodeForCoders.BffAdmin.Application;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Infra.Data;
using CodeForCoders.BffAdmin.Infra.Messaging;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffAdmin.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddBffAdminConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddBffProxyConfiguration(builder.Configuration);
        builder.AddStaffIdentityConfiguration();
        builder.Services.AddOptions<CommerceApiOptions>()
            .Bind(builder.Configuration.GetSection(CommerceApiOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && options.BaseAddress.EndsWith("/", StringComparison.Ordinal),
                "Commerce base address must be an absolute HTTP(S) URL ending in a slash.")
            .ValidateOnStart();
        builder.Services.AddHttpClient<ICommerceFinanceAreaClient, CommerceFinanceAreaClient>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<CommerceApiOptions>>().Value;
                client.BaseAddress = new Uri(settings.BaseAddress, UriKind.Absolute);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddHttpClient("bff-admin-outbound")
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
            });
        return builder;
    }
}
