using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CodeForCoders.Audit.Infra.Messaging;

public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private IConnection? connection;
    private bool disposed;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (connection is { IsOpen: true })
        {
            return connection;
        }

        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (connection is { IsOpen: true })
            {
                return connection;
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
            }

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = settings.Host,
                Port = settings.Port,
                UserName = settings.Username,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost,
                ClientProvidedName = AuditTelemetry.ServiceName,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };
            connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async Task<IChannel> CreatePublisherChannelAsync(CancellationToken cancellationToken)
    {
        var currentConnection = await GetConnectionAsync(cancellationToken);
        return await currentConnection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: null),
            cancellationToken);
    }

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        var currentConnection = await GetConnectionAsync(cancellationToken);
        return await currentConnection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await connectionLock.WaitAsync();
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (connection is not null)
            {
                await connection.DisposeAsync();
                connection = null;
            }
        }
        finally
        {
            connectionLock.Release();
            connectionLock.Dispose();
        }
    }
}
