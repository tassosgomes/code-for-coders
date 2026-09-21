using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.BffStudent.Infra.Messaging.Health;

public sealed class RabbitMqHealthCheck(RabbitMqConnectionProvider connectionProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BrokerUnreachableException exception)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is not reachable.", exception);
        }
    }
}
