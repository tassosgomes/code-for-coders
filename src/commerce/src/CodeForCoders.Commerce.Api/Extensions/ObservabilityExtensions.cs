using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Infra.Messaging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CodeForCoders.Commerce.Api.Extensions;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservabilityConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"]
            ?? throw new InvalidOperationException("OpenTelemetry:ServiceName is required.");
        var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment.name", environment.EnvironmentName),
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource(CommerceTelemetry.ActivitySourceName)
                .AddSource(RabbitMqTelemetry.SourceName)
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(CommerceTelemetry.MeterName)
                .AddOtlpExporter());

        services.AddLogging(logging => logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
            options.AddOtlpExporter();
        }));

        return services;
    }
}
