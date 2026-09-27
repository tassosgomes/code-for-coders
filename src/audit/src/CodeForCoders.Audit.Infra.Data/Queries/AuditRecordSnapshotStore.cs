using System.Text.Json;
using CodeForCoders.Audit.Application.Exceptions;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Infra.Data.Configuration;
using CodeForCoders.Audit.Infra.Data.Health;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CodeForCoders.Audit.Infra.Data.Queries;

public sealed class AuditRecordSnapshotStore(
    AuditSnapshotConnectionProvider connectionProvider,
    IOptions<AuditSnapshotOptions> options) : IAuditRecordSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> TryCreateAsync(
        string snapshotId,
        AuditRecordSnapshot snapshot,
        TimeSpan timeToLive,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = await connectionProvider.GetAsync(cancellationToken);
            var value = JsonSerializer.Serialize(snapshot, JsonOptions);
            return await connection.GetDatabase().StringSetAsync(
                GetKey(snapshotId),
                value,
                timeToLive,
                When.NotExists);
        }
        catch (RedisException)
        {
            throw new AuditSnapshotUnavailableException();
        }
    }

    public async Task<AuditRecordSnapshot?> FindAsync(string snapshotId, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await connectionProvider.GetAsync(cancellationToken);
            var value = await connection.GetDatabase().StringGetAsync(GetKey(snapshotId));
            if (value.IsNull)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<AuditRecordSnapshot>(value.ToString(), JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
        catch (RedisException)
        {
            throw new AuditSnapshotUnavailableException();
        }
    }

    private RedisKey GetKey(string snapshotId)
        => $"{options.Value.KeyPrefix}{snapshotId}";
}
