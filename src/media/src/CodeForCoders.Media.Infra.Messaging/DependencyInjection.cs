using CodeForCoders.Media.Infra.Messaging.Configuration;
using CodeForCoders.Media.Infra.Messaging.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Media.Infra.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<HeartbeatReceiptStore>();
        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddHostedService<OutboxPublisherWorker>();
        if (MediaRoleOptions.ReadRole(configuration) == MediaServiceRole.Api)
        {
            services.AddHostedService<HeartbeatConsumerWorker>();
        }
        else
        {
            services.AddHostedService<ExpiredVideoUploadWorker>();
        }

        return services;
    }
}
