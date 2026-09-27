using CodeForCoders.Audit.Infra.Data.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CodeForCoders.Audit.Infra.Data.Health;

public sealed class AuditSnapshotConnectionProvider(IOptions<AuditSnapshotOptions> options) : IAsyncDisposable
{
    private readonly Lazy<Task<IConnectionMultiplexer>> connection = new(
        async () => await ConnectionMultiplexer.ConnectAsync(options.Value.ConnectionString));

    public Task<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken)
        => connection.Value.WaitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (connection.IsValueCreated)
        {
            var multiplexer = await connection.Value;
            await multiplexer.CloseAsync();
            multiplexer.Dispose();
        }
    }
}
