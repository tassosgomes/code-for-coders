using CodeForCoders.BffAdmin.Application;
using CodeForCoders.BffAdmin.Infra.Data;
using CodeForCoders.BffAdmin.Infra.Messaging;

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
