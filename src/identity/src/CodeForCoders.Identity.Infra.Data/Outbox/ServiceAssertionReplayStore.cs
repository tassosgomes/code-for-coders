using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Infra.Data.Health;
using StackExchange.Redis;

namespace CodeForCoders.Identity.Infra.Data.Outbox;

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
            $"identity:service-assertion:{assertionId:N}",
            "consumed",
            ttl,
            When.NotExists);
    }
}
