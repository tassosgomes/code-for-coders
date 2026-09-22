using CodeForCoders.Notification.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CodeForCoders.Notification.Infra.Data.Health;

public sealed class OutboxHealthCheck(
    NotificationDbContext dbContext,
    ITenantContext tenantContext) : IHealthCheck
{
    private static readonly TimeSpan MaximumPendingAge = TimeSpan.FromMinutes(5);
    private const int MaximumAttempts = 10;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var pending = dbContext.OutboxMessages
            .Where(message => message.Namespace == tenantContext.Namespace
                && message.ProcessedOn == null);
        var exhausted = await pending.CountAsync(message => message.Attempts >= MaximumAttempts, cancellationToken);
        var oldest = await pending
            .Select(message => (DateTimeOffset?)message.OccurredOn)
            .MinAsync(cancellationToken);

        var data = new Dictionary<string, object>
        {
            ["exhausted"] = exhausted,
            ["oldestPendingAgeSeconds"] = oldest is null
                ? 0
                : Math.Max(0, (DateTimeOffset.UtcNow - oldest.Value).TotalSeconds),
        };

        if (exhausted > 0 || oldest <= DateTimeOffset.UtcNow - MaximumPendingAge)
        {
            return HealthCheckResult.Degraded("Outbox has delayed or exhausted messages.", data: data);
        }

        return HealthCheckResult.Healthy(data: data);
    }
}
