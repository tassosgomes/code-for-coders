using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Infra.Data.Health;
using StackExchange.Redis;

namespace CodeForCoders.Billing.Infra.Data.Outbox;

public sealed class ServiceAssertionReplayStore(
    ValkeyConnectionProvider connectionProvider,
    TimeProvider timeProvider)
    : IServiceAssertionReplayStore
{
    public async Task<bool> TryConsumeAsync(Guid assertionId, DateTimeOffset expiresOn, CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetAsync(cancellationToken);
        var ttl = expiresOn - timeProvider.GetUtcNow();
        if (ttl <= TimeSpan.Zero)
        {
            return false;
        }

        var database = connection.GetDatabase();
        return await database.StringSetAsync(
            $"billing:service-assertion:{assertionId:N}",
            "consumed",
            ttl,
            When.NotExists);
    }
}
