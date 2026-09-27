using System.Net;
using System.Net.Sockets;
using System.Text;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Infra.Data;
using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using CodeForCoders.BffAdmin.Infra.Data.Idempotency;
using CodeForCoders.BffAdmin.Infra.Data.Outbox;
using CodeForCoders.BffAdmin.Infra.Messaging;
using CodeForCoders.BffAdmin.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

[Collection(BffAdminIntegrationCollection.Name)]
public sealed class AuditComplementPublicationTests(BffAdminIntegrationFixture fixture)
{
    private const string Explanation = "The access change was verified against the internal support case.";
    private const string AuditRoutingKey = "auditoria.registro.complemento-confirmado.v1";

    [Fact(DisplayName = nameof(PublishesDecryptedConfirmationToTheAuditExchangeAndMarksItProcessed))]
    public async Task PublishesDecryptedConfirmationToTheAuditExchangeAndMarksItProcessed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = CreateOptions();
        var queue = NewQueueName();
        await DeclareDestinationAsync(CreateAdminOptions(), options.Exchange, queue, AuditRoutingKey, cancellationToken);
        var draft = CreateDraft(Guid.CreateVersion7());
        await ConfirmAsync(draft, cancellationToken);

        await RunWorkerUntilAsync(options, () => IsProcessedAsync(draft.ConfirmationId), cancellationToken);
        var received = await ReceiveOneAsync(CreateAdminOptions(), queue, cancellationToken);
        var stored = await ReadOutboxAsync(draft.ConfirmationId, cancellationToken);

        Assert.Equal(draft.ConfirmationId.ToString(), received.MessageId);
        Assert.Equal("ComplementoConfirmado", received.Type);
        Assert.Equal(options.Exchange, received.Exchange);
        Assert.Equal(AuditRoutingKey, received.RoutingKey);
        Assert.Contains(Explanation, received.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Explanation, stored.Payload, StringComparison.Ordinal);
        Assert.NotNull(stored.ProcessedOn);
        Assert.Null(stored.LeaseToken);
        Assert.Null(stored.LeaseExpiresOn);
    }

    [Fact(DisplayName = nameof(LeavesCommittedMessagePendingWhileBrokerIsUnavailableAndPublishesAfterRecovery))]
    public async Task LeavesCommittedMessagePendingWhileBrokerIsUnavailableAndPublishesAfterRecovery()
    {
        const int maxAttempts = 2;
        var cancellationToken = TestContext.Current.CancellationToken;
        var draft = CreateDraft(Guid.CreateVersion7());
        await ConfirmAsync(draft, cancellationToken);
        var unavailable = CreateOptions(port: GetClosedLocalPort());
        unavailable.Host = "127.0.0.1";
        string? finalState = null;

        try
        {
            await RunWorkerUntilAsync(unavailable, async failures =>
            {
                var message = await ReadOutboxAsync(draft.ConfirmationId, cancellationToken);
                finalState = $"failures={failures}, attempts={message.Attempts}, processed={message.ProcessedOn}, leaseToken={message.LeaseToken}, lastError={message.LastError}";
                return failures > maxAttempts + 1 && message.LeaseToken is null && message.LastError is not null;
            }, cancellationToken, timeoutSeconds: 60, maxAttempts: maxAttempts);
        }
        catch (TimeoutException exception)
        {
            throw new InvalidOperationException($"Outbox state at timeout: {finalState ?? "unavailable"}.", exception);
        }

        var pending = await ReadOutboxAsync(draft.ConfirmationId, cancellationToken);
        Assert.Null(pending.ProcessedOn);
        Assert.Equal(0, pending.Attempts);
        Assert.Equal("OUTBOX_BROKER_UNAVAILABLE", pending.LastError);

        var options = CreateOptions();
        var queue = NewQueueName();
        await DeclareDestinationAsync(CreateAdminOptions(), options.Exchange, queue, AuditRoutingKey, cancellationToken);
        await RunWorkerUntilAsync(
            options,
            _ => IsProcessedAsync(draft.ConfirmationId),
            cancellationToken,
            maxAttempts: maxAttempts);

        var received = await ReceiveOneAsync(CreateAdminOptions(), queue, cancellationToken);
        Assert.Equal(draft.ConfirmationId.ToString(), received.MessageId);
        Assert.Null(await ReceiveMaybeOneAsync(CreateAdminOptions(), queue, cancellationToken));
    }

    [Fact(DisplayName = nameof(ReclaimsAnExpiredLeaseAfterAWorkerStops))]
    public async Task ReclaimsAnExpiredLeaseAfterAWorkerStops()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = CreateOptions();
        var queue = NewQueueName();
        await DeclareDestinationAsync(CreateAdminOptions(), options.Exchange, queue, AuditRoutingKey, cancellationToken);
        var draft = CreateDraft(Guid.CreateVersion7());
        await ConfirmAsync(draft, cancellationToken);
        await using (var dbContext = CreateDbContext(draft.TenantId))
        {
            var message = await dbContext.OutboxMessages.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == draft.ConfirmationId, cancellationToken);
            message.AcquireLease(Guid.CreateVersion7(), TimeProvider.System.GetUtcNow().AddSeconds(-1));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await RunWorkerUntilAsync(options, () => IsProcessedAsync(draft.ConfirmationId), cancellationToken);
        var received = await ReceiveOneAsync(CreateAdminOptions(), queue, cancellationToken);

        Assert.Equal(draft.ConfirmationId.ToString(), received.MessageId);
        Assert.NotNull((await ReadOutboxAsync(draft.ConfirmationId, cancellationToken)).ProcessedOn);
    }

    [Fact(DisplayName = nameof(UsesMandatoryPublisherConfirmWhenDestinationHasNoBinding))]
    public async Task UsesMandatoryPublisherConfirmWhenDestinationHasNoBinding()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = CreateOptions();
        options.Exchange = $"audit.events.unroutable.{Guid.CreateVersion7():N}";
        options.Username = "code_for_coders";
        options.Password = "code_for_coders";
        await DeclareExchangeAsync(CreateAdminOptions(), options.Exchange, cancellationToken);
        var draft = CreateDraft(Guid.CreateVersion7());
        await ConfirmAsync(draft, cancellationToken);
        await using (var dbContext = CreateDbContext(draft.TenantId))
        {
            var message = await dbContext.OutboxMessages.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == draft.ConfirmationId, cancellationToken);
            dbContext.Entry(message).Property(item => item.DestinationExchange).CurrentValue = options.Exchange;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await RunWorkerUntilAsync(options, async () =>
        {
            var message = await ReadOutboxAsync(draft.ConfirmationId, cancellationToken);
            return message.Attempts > 0;
        }, cancellationToken);

        var pending = await ReadOutboxAsync(draft.ConfirmationId, cancellationToken);
        Assert.Null(pending.ProcessedOn);
        Assert.Equal("OUTBOX_PUBLISH_FAILED", pending.LastError);
        Assert.Null(pending.LeaseToken);

        await using var cleanupContext = CreateDbContext(draft.TenantId);
        var unroutableMessage = await cleanupContext.OutboxMessages.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == draft.ConfirmationId, cancellationToken);
        unroutableMessage.MarkProcessed(TimeProvider.System.GetUtcNow());
        await cleanupContext.SaveChangesAsync(cancellationToken);
    }

    [Fact(DisplayName = nameof(ContinuesPublishingLegacyMessagesToTheBffExchange))]
    public async Task ContinuesPublishingLegacyMessagesToTheBffExchange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = CreateOptions();
        options.Exchange = "bff-admin.events";
        var queue = NewQueueName();
        const string routingKey = "bff-admin.integration.legacy.v1";
        await DeclareDestinationAsync(CreateAdminOptions(), options.Exchange, queue, routingKey, cancellationToken);
        var tenantId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();
        await using (var dbContext = CreateDbContext(tenantId))
        {
            var protector = CreateProtector();
            await new OutboxMessageWriter(dbContext, protector).AppendAsync(
                new OutboxMessageDraft(
                    messageId,
                    tenantId,
                    "LegacyBffEvent",
                    routingKey,
                    new { eventId = messageId, tenantId },
                    TimeProvider.System.GetUtcNow(),
                    null),
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await RunWorkerUntilAsync(options, () => IsProcessedAsync(messageId), cancellationToken);
        var received = await ReceiveOneAsync(CreateAdminOptions(), queue, cancellationToken);

        Assert.Equal(messageId.ToString(), received.MessageId);
        Assert.Equal(options.Exchange, received.Exchange);
        Assert.Equal(routingKey, received.RoutingKey);
    }

    [Fact(DisplayName = nameof(CannotReadTheAuditOwnedComplementQueue))]
    public async Task CannotReadTheAuditOwnedComplementQueue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = $"audit.complements.permission-test.{Guid.CreateVersion7():N}";
        var adminOptions = CreateAdminOptions();
        var factory = CreateConnectionFactory(adminOptions);
        await using (var adminConnection = await factory.CreateConnectionAsync(cancellationToken))
        await using (var adminChannel = await adminConnection.CreateChannelAsync(cancellationToken: cancellationToken))
        {
            await adminChannel.QueueDeclareAsync(
                queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }

        await using var restrictedConnection = new RabbitMqConnectionProvider(Options.Create(CreateOptions()));
        var connection = await restrictedConnection.GetConnectionAsync(cancellationToken);
        await using var restrictedChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var exception = await Assert.ThrowsAsync<OperationInterruptedException>(async () =>
            await restrictedChannel.BasicGetAsync(queue, autoAck: true, cancellationToken: cancellationToken));

        Assert.Contains("ACCESS_REFUSED", exception.Message, StringComparison.Ordinal);
    }

    private async Task ConfirmAsync(AuditComplementConfirmationDraft draft, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(draft.TenantId);
        var protector = CreateProtector();
        var store = new AuditComplementConfirmationStore(
            dbContext,
            new OutboxMessageWriter(dbContext, protector),
            protector);
        var result = await store.ConfirmAsync(draft, cancellationToken);
        Assert.Equal(AuditComplementConfirmationWriteStatus.Accepted, result.Status);
    }

    private Task RunWorkerUntilAsync(
        RabbitMqOptions rabbitOptions,
        Func<Task<bool>> condition,
        CancellationToken cancellationToken,
        int timeoutSeconds = 10)
        => RunWorkerUntilAsync(rabbitOptions, _ => condition(), cancellationToken, timeoutSeconds);

    private async Task RunWorkerUntilAsync(
        RabbitMqOptions rabbitOptions,
        Func<int, Task<bool>> condition,
        CancellationToken cancellationToken,
        int timeoutSeconds = 10,
        int maxAttempts = 10)
    {
        await using var connectionProvider = new RabbitMqConnectionProvider(Options.Create(rabbitOptions));
        var dbOptions = new DbContextOptionsBuilder<BffAdminDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        await using var serviceProvider = new ServiceCollection()
            .AddScoped(_ => new BffAdminDbContext(dbOptions, new TenantContext()))
            .BuildServiceProvider();
        var workerLogger = new RecordingLogger<OutboxPublisherWorker>();
        var worker = new OutboxPublisherWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new RabbitMqPublisher(connectionProvider, Options.Create(rabbitOptions)),
            CreateProtector(),
            Options.Create(new OutboxOptions
            {
                PollingIntervalSeconds = 1,
                BatchSize = 1,
                MaxAttempts = maxAttempts,
                LeaseDurationSeconds = 5,
            }),
            workerLogger);
        await worker.StartAsync(cancellationToken);
        try
        {
            var deadline = TimeProvider.System.GetUtcNow().AddSeconds(timeoutSeconds);
            while (TimeProvider.System.GetUtcNow() < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (worker.ExecuteTask is { IsFaulted: true } workerTask)
                {
                    await workerTask;
                }

                if (await condition(workerLogger.FailureCount))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }

            throw new TimeoutException(
                $"The outbox worker did not reach the expected state. Last worker error: {workerLogger.LastException}",
                workerLogger.LastException);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    private async Task<bool> IsProcessedAsync(Guid messageId)
        => (await ReadOutboxAsync(messageId, TestContext.Current.CancellationToken)).ProcessedOn is not null;

    private async Task<OutboxMessage> ReadOutboxAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(Guid.Empty);
        return await dbContext.OutboxMessages.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(message => message.Id == messageId, cancellationToken);
    }

    private async Task DeclareDestinationAsync(
        RabbitMqOptions options,
        string exchange,
        string queue,
        string routingKey,
        CancellationToken cancellationToken)
    {
        await DeclareExchangeAsync(options, exchange, cancellationToken);
        var factory = CreateConnectionFactory(options);
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, exchange, routingKey, arguments: null, cancellationToken: cancellationToken);
    }

    private static async Task DeclareExchangeAsync(
        RabbitMqOptions options,
        string exchange,
        CancellationToken cancellationToken)
    {
        var factory = CreateConnectionFactory(options);
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task<ReceivedMessage> ReceiveOneAsync(
        RabbitMqOptions options,
        string queue,
        CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(5);
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            var result = await ReceiveMaybeOneAsync(options, queue, cancellationToken);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The expected message was not published to RabbitMQ.");
    }

    private async Task<ReceivedMessage?> ReceiveMaybeOneAsync(
        RabbitMqOptions options,
        string queue,
        CancellationToken cancellationToken)
    {
        var factory = CreateConnectionFactory(options);
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var result = await channel.BasicGetAsync(queue, autoAck: false, cancellationToken: cancellationToken);
        if (result is null)
        {
            return null;
        }

        await channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
        return new ReceivedMessage(
            result.BasicProperties.MessageId,
            result.BasicProperties.Type,
            result.Exchange,
            result.RoutingKey,
            Encoding.UTF8.GetString(result.Body.Span));
    }

    private BffAdminDbContext CreateDbContext(Guid tenantId)
    {
        var context = new TenantContext();
        if (tenantId != Guid.Empty)
        {
            context.Set(tenantId);
        }

        var dbOptions = new DbContextOptionsBuilder<BffAdminDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        return new BffAdminDbContext(dbOptions, context);
    }

    private RabbitMqOptions CreateOptions(int? port = null)
        => new()
        {
            Host = fixture.RabbitMq.Hostname,
            Port = port ?? fixture.RabbitMq.GetMappedPublicPort(5672),
            Username = BffAdminIntegrationFixture.PublisherUsername,
            Password = BffAdminIntegrationFixture.PublisherPassword,
            VirtualHost = "/",
            Exchange = "audit.events",
            DeadLetterExchange = "audit.events.dlx",
            HeartbeatQueue = "bff-admin.platform-heartbeat",
        };

    private RabbitMqOptions CreateAdminOptions()
        => new()
        {
            Host = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            Username = "code_for_coders",
            Password = "code_for_coders",
            VirtualHost = "/",
            Exchange = "audit.events",
            DeadLetterExchange = "audit.events.dlx",
            HeartbeatQueue = "bff-admin.platform-heartbeat",
        };

    private static ConnectionFactory CreateConnectionFactory(RabbitMqOptions options)
        => new()
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
        };

    private static OutboxPayloadProtector CreateProtector()
        => new(Options.Create(new OutboxProtectionOptions
        {
            KeyBase64 = SharedOutboxTestProtection.KeyBase64,
            KeyVersion = SharedOutboxTestProtection.KeyVersion,
        }));

    private static int GetClosedLocalPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string NewQueueName() => $"audit.complements.integration.{Guid.CreateVersion7():N}";

    private static AuditComplementConfirmationDraft CreateDraft(Guid tenantId)
    {
        var confirmedAt = TimeProvider.System.GetUtcNow();
        return new AuditComplementConfirmationDraft(
            tenantId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(confirmedAt),
            Guid.CreateVersion7(confirmedAt),
            Explanation,
            confirmedAt,
            null);
    }

    private sealed record ReceivedMessage(
        string? MessageId,
        string? Type,
        string Exchange,
        string RoutingKey,
        string Body);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private int failureCount;

        public Exception? LastException { get; private set; }

        public int FailureCount => Volatile.Read(ref failureCount);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
            {
                Interlocked.Increment(ref failureCount);
                LastException = exception;
            }
        }
    }
}
