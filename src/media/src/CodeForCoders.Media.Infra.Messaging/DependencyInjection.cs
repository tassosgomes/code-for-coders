using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using CodeForCoders.Media.Infra.Messaging.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

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
            .Validate(options => options.HasValidManagementUri(), "RabbitMQ Management URI must be an absolute HTTP or HTTPS URI without user information.")
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
            services.AddOptions<VideoPreparationOptions>()
                .Bind(configuration.GetSection(VideoPreparationOptions.SectionName))
                .Validate(options => options.HasValidWorkerSettings(), "Media video preparation configuration is invalid.")
                .ValidateOnStart();
            services.AddSingleton<IVideoKeyProtector, AesVideoKeyProtector>();
            services.AddSingleton<IVideoTranscoder, FfmpegVideoTranscoder>();
            services.AddHostedService<ExpiredVideoUploadWorker>();
            services.AddHostedService<VideoPreparationWorker>();
            services.AddHostedService<MediaVolumeMetricsWorker>();
            services.AddHttpClient<RabbitMqManagementClient>()
                .AddStandardResilienceHandler(options =>
                {
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                    options.Retry.MaxRetryAttempts = 3;
                    options.Retry.DisableForUnsafeHttpMethods();
                });
            services.AddHostedService<RabbitMqDeadLetterMetricsWorker>();
        }

        return services;
    }
}
