using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using CodeForCoders.Audit.Application;
using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Infra.Data;
using CodeForCoders.Audit.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Audit.IntegrationTests;

[Collection(AuditIntegrationCollection.Name)]
public sealed class AuditDeadLetterTests(AuditIntegrationFixture fixture)
{
    private const string Exchange = "audit.integration.dead-letter-events";
    private const string RoutingKey = "auditoria.ato-praticado.v1";
    private const string Queue = "audit.integration.dead-letter-acts";
    private const string DeadLetterQueue = "audit.integration.dead-letter-acts.dlq";
    private const string DatabaseFailureQueue = "audit.integration.database-failure-acts";
    private const string DatabaseFailureDeadLetterQueue = "audit.integration.database-failure-acts.dlq";
    private const string PrivatePayload = "private-payload-never-log";
    private static readonly DateTimeOffset ReceivedOn = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = nameof(SendsMalformedJsonToTheDeadLetterQueueIntact))]
    public async Task SendsMalformedJsonToTheDeadLetterQueueIntact()
    {
        var body = Encoding.UTF8.GetBytes($"{{\"private\":\"{PrivatePayload}\"");

        await AssertDeadLetteredIntactAsync(body, "body", PrivatePayload);
    }

    [Fact(DisplayName = nameof(SendsMessageWithoutFactIdToTheDeadLetterQueueIntact))]
    public async Task SendsMessageWithoutFactIdToTheDeadLetterQueueIntact()
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            origem = "identidade",
            tenantId = Guid.CreateVersion7(),
        });

        await AssertDeadLetteredIntactAsync(body, "fatoId");
    }

    [Fact(DisplayName = nameof(SendsMessageWithoutOriginToTheDeadLetterQueueIntact))]
    public async Task SendsMessageWithoutOriginToTheDeadLetterQueueIntact()
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            fatoId = Guid.CreateVersion7(),
            tenantId = Guid.CreateVersion7(),
        });

        await AssertDeadLetteredIntactAsync(body, "origem");
    }

    [Fact(DisplayName = nameof(SendsOriginOutsideTheContractPatternToTheDeadLetterQueueIntact))]
    public async Task SendsOriginOutsideTheContractPatternToTheDeadLetterQueueIntact()
    {
        var body = CreateActBody(Guid.CreateVersion7(), "Identidade");

        await AssertDeadLetteredIntactAsync(body, "origem");
    }

    [Fact(DisplayName = nameof(SendsOriginLongerThanTheContractLimitToTheDeadLetterQueueIntact))]
    public async Task SendsOriginLongerThanTheContractLimitToTheDeadLetterQueueIntact()
    {
        var body = CreateActBody(Guid.CreateVersion7(), new string('a', 101));

        await AssertDeadLetteredIntactAsync(body, "origem");
    }

    [Fact(DisplayName = nameof(SendsMessageWithoutTenantIdToTheDeadLetterQueueIntact))]
    public async Task SendsMessageWithoutTenantIdToTheDeadLetterQueueIntact()
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            fatoId = Guid.CreateVersion7(),
            origem = "identidade",
        });

        await AssertDeadLetteredIntactAsync(body, "tenantId");
    }

    [Fact(DisplayName = nameof(RetriesDatabaseFailuresAndReprocessesTheDeadLetterMessageIdempotently))]
    public async Task RetriesDatabaseFailuresAndReprocessesTheDeadLetterMessageIdempotently()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var factId = Guid.CreateVersion7();
        var body = CreateActBody(factId, "identidade");
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.PostgreSql.GetConnectionString())
        {
            Timeout = 1,
        }.ConnectionString;
        using var metrics = new RedeliveryMetricCapture();
        using var logs = new CapturingLoggerProvider();
        using var processingBarrier = new ConsumerActivityBarrier();
        IHost? host = await StartHostAsync(
            logs,
            cancellationToken,
            connectionString,
            DatabaseFailureQueue);
        var databaseStopped = false;
        try
        {
            await WaitForConsumerCountAsync(cancellationToken, DatabaseFailureQueue);
            await PublishAsync(body, cancellationToken);
            await processingBarrier.WaitForStartedAsync(cancellationToken);
            await fixture.PostgreSql.StopAsync(cancellationToken);
            databaseStopped = true;
            processingBarrier.Release();
            try
            {
                await WaitForQueueMessageCountAsync(DatabaseFailureDeadLetterQueue, 1, cancellationToken);
            }
            catch (TimeoutException exception)
            {
                var mainQueueCount = await GetQueueMessageCountAsync(DatabaseFailureQueue, cancellationToken);
                var deadLetterQueueCount = await GetQueueMessageCountAsync(
                    DatabaseFailureDeadLetterQueue,
                    cancellationToken);
                var retriesLogged = logs.Events.Count(entry => entry.Message.StartsWith(
                    "Unexpected failure while recording an administrative act",
                    StringComparison.Ordinal));
                var queueState = await GetQueueStateAsync(DatabaseFailureQueue, cancellationToken);
                throw new TimeoutException(
                    $"{exception.Message} Main queue count: {mainQueueCount}; dead-letter queue count: "
                    + $"{deadLetterQueueCount}; queue state: {queueState}; retry logs: {retriesLogged}",
                    exception);
            }

            await host.StopAsync(CancellationToken.None);
            host.Dispose();
            host = null;

            await fixture.PostgreSql.StartAsync(cancellationToken);
            databaseStopped = false;
            await WaitForDatabaseAsync(cancellationToken);

            host = await StartHostAsync(logs, cancellationToken, queueName: DatabaseFailureQueue);
            await WaitForConsumerCountAsync(cancellationToken, DatabaseFailureQueue);

            var deadLetterBody = await TakeDeadLetterBodyAsync(
                cancellationToken,
                DatabaseFailureDeadLetterQueue);
            Assert.Equal(body, deadLetterBody);

            await PublishAsync(deadLetterBody, cancellationToken);
            try
            {
                await WaitForFactRecordCountAsync(factId, 1, cancellationToken);
            }
            catch (TimeoutException exception)
            {
                var retries = string.Join(
                    Environment.NewLine,
                    logs.Events.Where(entry => entry.Message.StartsWith(
                            "Unexpected failure while recording an administrative act",
                            StringComparison.Ordinal))
                        .Take(5)
                        .Select(entry => entry.Message));
                var queueState = await GetQueueStateAsync(DatabaseFailureQueue, cancellationToken);
                throw new TimeoutException(
                    $"{exception.Message} Queue state: {queueState}; retry errors: {retries}",
                    exception);
            }

            await PublishAsync(deadLetterBody, cancellationToken);
            await metrics.WaitForIdenticalRedeliveryAsync(cancellationToken);

            Assert.Equal(1, await GetFactRecordCountAsync(factId, cancellationToken));
            Assert.Equal(0u, await GetQueueMessageCountAsync(DatabaseFailureDeadLetterQueue, cancellationToken));
        }
        finally
        {
            processingBarrier.Release();
            try
            {
                if (host is not null)
                {
                    await host.StopAsync(CancellationToken.None);
                }
            }
            finally
            {
                host?.Dispose();
                if (databaseStopped)
                {
                    await fixture.PostgreSql.StartAsync(CancellationToken.None);
                }
            }
        }
    }

    private async Task AssertDeadLetteredIntactAsync(byte[] body, string expectedReason, string? privateText = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var originalRecordCount = await GetRecordCountAsync(cancellationToken);
        using var metrics = new IllegibleMetricCapture();
        using var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logs, cancellationToken);
        await WaitForConsumerCountAsync(cancellationToken);

        await PublishAsync(body, cancellationToken);
        var deadLetterBody = await TakeDeadLetterBodyAsync(cancellationToken);
        await metrics.WaitForReasonAsync(expectedReason, cancellationToken);

        Assert.Equal(body, deadLetterBody);
        Assert.Equal(originalRecordCount, await GetRecordCountAsync(cancellationToken));
        Assert.Equal(0u, await GetQueueMessageCountAsync(DeadLetterQueue, cancellationToken));
        Assert.Single(metrics.Events, metric => metric.Tags["reason"] == expectedReason);

        if (privateText is not null)
        {
            var logText = string.Join(Environment.NewLine, logs.Events.Select(entry => entry.Message));
            Assert.DoesNotContain(privateText, logText, StringComparison.Ordinal);
        }
    }

    private static byte[] CreateActBody(Guid factId, string origin)
        => JsonSerializer.SerializeToUtf8Bytes(new
        {
            fatoId = factId,
            origem = origin,
            tenantId = Guid.CreateVersion7(),
        });

    private async Task<IHost> StartHostAsync(
        ILoggerProvider? loggerProvider,
        CancellationToken cancellationToken,
        string? connectionString = null,
        string queueName = Queue)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString ?? fixture.PostgreSql.GetConnectionString(),
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = Exchange,
            ["RabbitMq:DeadLetterExchange"] = "audit.integration.dead-letter-events.dlx",
            ["RabbitMq:AuditQueue"] = queueName,
            ["RabbitMq:EventRoutingKey"] = RoutingKey,
            ["AuditDatabase:WriterRole"] = "code_for_coders_audit_writer",
        };

        var host = Host.CreateDefaultBuilder()
            .UseEnvironment("IntegrationTest")
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(configurationValues))
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                if (loggerProvider is not null)
                {
                    logging.AddProvider(loggerProvider);
                }

                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationConfiguration();
                services.AddDataConfiguration(context.Configuration, context.HostingEnvironment);
                services.AddMessagingConfiguration(context.Configuration);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(ReceivedOn));
            })
            .Build();

        await host.StartAsync(cancellationToken);
        return host;
    }

    private async Task PublishAsync(byte[] body, CancellationToken cancellationToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            UserName = "code_for_coders",
            Password = "code_for_coders",
        };
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Type = "AtoPraticado",
        };
        await channel.BasicPublishAsync(
            Exchange,
            RoutingKey,
            mandatory: true,
            properties,
            body,
            cancellationToken);
    }

    private async Task<byte[]> TakeDeadLetterBodyAsync(
        CancellationToken cancellationToken,
        string deadLetterQueue = DeadLetterQueue)
    {
        await using var connection = await CreateRabbitConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        for (var attempt = 0; attempt < 400; attempt++)
        {
            var delivery = await channel.BasicGetAsync(deadLetterQueue, autoAck: false, cancellationToken);
            if (delivery is not null)
            {
                var body = delivery.Body.ToArray();
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                return body;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The audit message did not reach its dead-letter queue.");
    }

    private async Task WaitForQueueMessageCountAsync(
        string queue,
        uint expectedCount,
        CancellationToken cancellationToken)
    {
        await using var connection = await CreateRabbitConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        for (var attempt = 0; attempt < 400; attempt++)
        {
            var queueInfo = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
            if (queueInfo.MessageCount >= expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException($"The expected message count was not reached in queue '{queue}'.");
    }

    private async Task WaitForConsumerCountAsync(
        CancellationToken cancellationToken,
        string queue = Queue)
    {
        await using var connection = await CreateRabbitConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var queueInfo = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
            if (queueInfo.ConsumerCount > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The audit RabbitMQ consumer did not start.");
    }

    private async Task<uint> GetQueueMessageCountAsync(string queue, CancellationToken cancellationToken)
    {
        await using var connection = await CreateRabbitConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var queueInfo = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
        return queueInfo.MessageCount;
    }

    private async Task<string> GetQueueStateAsync(string queue, CancellationToken cancellationToken)
    {
        var result = await fixture.RabbitMq.ExecAsync(
            ["rabbitmqctl", "list_queues", "name", "arguments", "messages_ready", "messages_unacknowledged"],
            cancellationToken);
        var queueDetails = result.Stdout
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains(queue, StringComparison.Ordinal));
        return $"rabbitmqctl exit={result.ExitCode}; {string.Join(Environment.NewLine, queueDetails)}";
    }

    private async Task<IConnection> CreateRabbitConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            UserName = "code_for_coders",
            Password = "code_for_coders",
        };
        return await connectionFactory.CreateConnectionAsync(cancellationToken);
    }

    private async Task WaitForDatabaseAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var dbContext = CreateDbContext();
            if (await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("The PostgreSQL fixture did not accept connections after restarting.");
    }

    private async Task WaitForFactRecordCountAsync(
        Guid factId,
        long expectedCount,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (await GetFactRecordCountAsync(factId, cancellationToken) == expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("The expected audit record count was not reached.");
    }

    private async Task<long> GetRecordCountAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.AuditRecords.LongCountAsync(cancellationToken);
    }

    private async Task<long> GetFactRecordCountAsync(Guid factId, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.AuditRecords
            .LongCountAsync(record => record.FactId == factId, cancellationToken);
    }

    private AuditDbContext CreateDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        return new AuditDbContext(dbOptions);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> events = new();

        public IReadOnlyCollection<LogEntry> Events => events.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(events);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<LogEntry> events) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var message = formatter(state, null);
                if (exception is not null)
                {
                    message = $"{message} {exception.GetType().Name}: {exception.Message}";
                }

                events.Enqueue(new LogEntry(logLevel, message));
            }
        }
    }

    private sealed class IllegibleMetricCapture : IDisposable
    {
        private readonly ConcurrentQueue<MetricEvent> events = new();
        private readonly MeterListener listener = new();

        public IReadOnlyCollection<MetricEvent> Events => events.ToArray();

        public IllegibleMetricCapture()
        {
            listener.InstrumentPublished = static (instrument, measurementListener) =>
            {
                if (instrument.Meter.Name == AuditTelemetry.MeterName
                    && instrument.Name == "audit.messages.illegible")
                {
                    measurementListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                var copiedTags = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var tag in tags)
                {
                    copiedTags[tag.Key] = tag.Value?.ToString();
                }

                events.Enqueue(new MetricEvent(instrument.Name, value, copiedTags));
            });
            listener.Start();
        }

        public async Task WaitForReasonAsync(string reason, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (Events.Any(entry => entry.Tags.TryGetValue("reason", out var value) && value == reason))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }

            throw new TimeoutException($"The illegible-message metric with reason '{reason}' was not recorded.");
        }

        public void Dispose() => listener.Dispose();
    }

    private sealed class RedeliveryMetricCapture : IDisposable
    {
        private readonly ConcurrentQueue<string> outcomes = new();
        private readonly MeterListener listener = new();

        public IReadOnlyCollection<string> Outcomes => outcomes.ToArray();

        public RedeliveryMetricCapture()
        {
            listener.InstrumentPublished = static (instrument, measurementListener) =>
            {
                if (instrument.Meter.Name == AuditTelemetry.MeterName
                    && instrument.Name == "audit.acts.redelivered")
                {
                    measurementListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome" && tag.Value?.ToString() is { } outcome)
                    {
                        outcomes.Enqueue(outcome);
                    }
                }
            });
            listener.Start();
        }

        public async Task WaitForIdenticalRedeliveryAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (Outcomes.Contains("identical", StringComparer.Ordinal))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }

            throw new TimeoutException("The duplicate audit message was not recognized as an identical redelivery.");
        }

        public void Dispose() => listener.Dispose();
    }

    private sealed class ConsumerActivityBarrier : IDisposable
    {
        private readonly TaskCompletionSource activityStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource continueProcessing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ActivityListener listener;

        public ConsumerActivityBarrier()
        {
            listener = new ActivityListener
            {
                ShouldListenTo = static source => source.Name == RabbitMqTelemetry.SourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = _ =>
                {
                    if (activityStarted.TrySetResult())
                    {
                        continueProcessing.Task.GetAwaiter().GetResult();
                    }
                },
            };
            ActivitySource.AddActivityListener(listener);
        }

        public Task WaitForStartedAsync(CancellationToken cancellationToken)
            => activityStarted.Task.WaitAsync(cancellationToken);

        public void Release() => continueProcessing.TrySetResult();

        public void Dispose()
        {
            Release();
            listener.Dispose();
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed record MetricEvent(string Name, long Value, IReadOnlyDictionary<string, string?> Tags);
}
