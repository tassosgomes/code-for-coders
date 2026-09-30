using CodeForCoders.Learning.Infra.Messaging.Configuration;
using CodeForCoders.Learning.Infra.Messaging.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Learning.Infra.Messaging;

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
        services.AddOptions<CatalogInitialLoadOptions>()
            .Bind(configuration.GetSection(CatalogInitialLoadOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<CatalogInitialLoad>();
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<HeartbeatReceiptStore>();
        services.AddScoped<VideoProjectionStore>();
        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddHostedService<OutboxPublisherWorker>();
        services.AddHostedService<HeartbeatConsumerWorker>();
        services.AddHostedService<VideoProjectionConsumerWorker>();

        return services;
    }
}
