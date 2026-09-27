using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CodeForCoders.Audit.Infra.Data.Health;

public sealed class AuditSnapshotHealthCheck(AuditSnapshotConnectionProvider connectionProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await connectionProvider.GetAsync(cancellationToken);
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedisException exception)
        {
            return HealthCheckResult.Unhealthy("Valkey is not reachable.", exception);
        }
    }
}
