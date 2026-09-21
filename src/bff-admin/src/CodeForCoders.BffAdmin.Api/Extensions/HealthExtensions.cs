using CodeForCoders.BffAdmin.Infra.Data.Health;
using CodeForCoders.BffAdmin.Infra.Messaging.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CodeForCoders.BffAdmin.Api.Extensions;

public static class HealthExtensions
{
    public static IServiceCollection AddHealthConfiguration(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
            .AddCheck<PostgresHealthCheck>("postgres", HealthStatus.Unhealthy, tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(5))
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", HealthStatus.Unhealthy, tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(5))
            .AddCheck<ValkeyHealthCheck>("valkey", HealthStatus.Degraded, tags: new[] { "ready" }, timeout: TimeSpan.FromSeconds(3))
            .AddCheck<OutboxHealthCheck>("outbox", HealthStatus.Degraded, tags: new[] { "ready" });
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
