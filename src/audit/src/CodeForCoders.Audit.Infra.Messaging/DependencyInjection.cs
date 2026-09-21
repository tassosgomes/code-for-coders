using CodeForCoders.Audit.Infra.Messaging.Configuration;
using CodeForCoders.Audit.Infra.Messaging.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Audit.Infra.Messaging;

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
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<AuditReceiptStore>();
        services.AddHostedService<AuditTopologyInitializer>();
        services.AddHostedService<AuditEventConsumerWorker>();

        return services;
    }
}
