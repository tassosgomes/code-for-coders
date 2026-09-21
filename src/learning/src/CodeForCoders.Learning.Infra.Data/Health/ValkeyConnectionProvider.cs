using Microsoft.Extensions.Options;
using StackExchange.Redis;
using CodeForCoders.Learning.Infra.Data.Configuration;

namespace CodeForCoders.Learning.Infra.Data.Health;

public sealed class ValkeyConnectionProvider(IOptions<ValkeyOptions> options) : IAsyncDisposable
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
