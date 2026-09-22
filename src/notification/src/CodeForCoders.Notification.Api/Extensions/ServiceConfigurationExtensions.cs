using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Infra.Data;
using CodeForCoders.Notification.Infra.Data.Adapters;
using CodeForCoders.Notification.Infra.Messaging;

namespace CodeForCoders.Notification.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddNotificationConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddNotificationMessageHandlers();
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddHttpClient<ITransactionalEmailSender, HttpTransactionalEmailSender>()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
            });
        return builder;
    }
}
