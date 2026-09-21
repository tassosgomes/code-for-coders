using CodeForCoders.Audit.Infra.Data.Health;
using CodeForCoders.Audit.Infra.Messaging.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CodeForCoders.Audit.Api.Extensions;

public static class HealthExtensions
{
    public static IServiceCollection AddHealthConfiguration(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: new[] { "live" })
            .AddCheck<PostgresHealthCheck>(
                "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" },
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<RabbitMqHealthCheck>(
                "rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" },
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
            ResponseWriter = WriteStatusAsync,
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteStatusAsync,
        });
    }

    private static Task WriteStatusAsync(HttpContext context, HealthReport report)
        => context.Response.WriteAsJsonAsync(
            new { status = report.Status.ToString().ToLowerInvariant() },
            cancellationToken: context.RequestAborted);
}
