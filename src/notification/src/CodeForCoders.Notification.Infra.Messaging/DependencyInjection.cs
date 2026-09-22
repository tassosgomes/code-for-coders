using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Infra.Messaging.Configuration;
using CodeForCoders.Notification.Infra.Messaging.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Notification.Infra.Messaging;

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
        services.AddOptions<DeliveryOptions>()
            .Bind(configuration.GetSection(DeliveryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqResourceNames>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<ITransactionalEmailRetryPolicy, TransactionalEmailRetryPolicy>();
        services.AddSingleton<HeartbeatReceiptStore>();
        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddHostedService<OutboxPublisherWorker>();
        services.AddHostedService<HeartbeatConsumerWorker>();
        services.AddHostedService<NotificationSendRequestConsumerWorker>();
        services.AddHostedService<TransactionalEmailDeliveryWorker>();

        return services;
    }
}
