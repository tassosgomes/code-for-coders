using System.Text.Json;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using CodeForCoders.BffStudent.Infra.Data.Configuration;
using CodeForCoders.BffStudent.Infra.Data.Health;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CodeForCoders.BffStudent.Infra.Data;

public sealed class ValkeyBffSessionStore(
    ValkeyConnectionProvider connectionProvider,
    IOptions<BffSecurityOptions> securityOptions) : IBffSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task StoreAsync(OpaqueBffSession session, CancellationToken cancellationToken)
    {
        var lifetime = session.ExpiresAt - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero)
        {
            return;
        }

        var connection = await connectionProvider.GetAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(session, JsonOptions);
        await connection.GetDatabase().StringSetAsync(
            GetKey(session.SessionId),
            payload,
            lifetime).WaitAsync(cancellationToken);
    }

    public async Task<OpaqueBffSession?> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetAsync(cancellationToken);
        var payload = await connection.GetDatabase().StringGetAsync(GetKey(sessionId)).WaitAsync(cancellationToken);
        return payload.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<OpaqueBffSession>(payload.ToString(), JsonOptions);
    }

    public async Task RemoveAsync(string sessionId, CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetAsync(cancellationToken);
        await connection.GetDatabase().KeyDeleteAsync(GetKey(sessionId)).WaitAsync(cancellationToken);
    }

    private RedisKey GetKey(string sessionId)
        => $"{securityOptions.Value.SessionKeyPrefix}{sessionId}";
}
