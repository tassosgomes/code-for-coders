using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data.Health;
using StackExchange.Redis;

namespace CodeForCoders.Commerce.Infra.Data.Outbox;

public sealed class ServiceAssertionReplayStore(
    ValkeyConnectionProvider connectionProvider,
    TimeProvider timeProvider)
    : IServiceAssertionReplayStore
{
    public async Task<bool> TryConsumeAsync(Guid assertionId, DateTimeOffset expiresOn, CancellationToken cancellationToken)
    {
        var ttl = expiresOn - timeProvider.GetUtcNow();
        if (ttl <= TimeSpan.Zero)
        {
            return false;
        }

        var connection = await connectionProvider.GetAsync(cancellationToken);
        return await connection.GetDatabase().StringSetAsync(
            $"commerce:service-assertion:{assertionId:N}",
            "consumed",
            ttl,
            When.NotExists);
    }
}
