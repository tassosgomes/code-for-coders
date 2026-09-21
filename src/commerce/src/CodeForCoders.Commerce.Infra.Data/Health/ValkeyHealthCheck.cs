using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace CodeForCoders.Commerce.Infra.Data.Health;

public sealed class ValkeyHealthCheck(ValkeyConnectionProvider connectionProvider) : IHealthCheck
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
        catch (RedisConnectionException exception)
        {
            return HealthCheckResult.Degraded("Valkey is not reachable.", exception);
        }
        catch (RedisTimeoutException exception)
        {
            return HealthCheckResult.Degraded("Valkey health check timed out.", exception);
        }
    }
}
