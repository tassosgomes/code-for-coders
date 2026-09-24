using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using CodeForCoders.Audit.Application;
using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Infra.Data;
using CodeForCoders.Audit.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Audit.IntegrationTests;

[Collection(AuditIntegrationCollection.Name)]
public sealed class NonConformingActRecordingTests(AuditIntegrationFixture fixture)
{
    private const string Exchange = "audit.integration.events";
    private const string RoutingKey = "auditoria.ato-praticado.v1";
    private const string Queue = "audit.integration.nonconforming-acts";
    private const string DeadLetterQueue = "audit.integration.nonconforming-acts.dlq";
    private static readonly DateTimeOffset ReceivedOn = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact(DisplayName = nameof(RecordsMissingRequiredReasonAndEmitsOneAlert))]
    public async Task RecordsMissingRequiredReasonAndEmitsOneAlert()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("papel-revogado") with { Motivo = null };
        using var metrics = new ActMetricCapture();
        using var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logs, cancellationToken);

        await PublishAsync(act, cancellationToken);
        var record = await WaitForRecordAsync(host, act.FatoId, cancellationToken);
        await metrics.WaitForCountAsync("audit.acts.nonconforming", 1, cancellationToken);
        await logs.WaitForWarningAsync(act.FatoId, cancellationToken);

        Assert.Equal(AuditRecord.NonConforming, record.Conformity);
        Assert.Equal(new[] { "motivo-ausente" }, record.Reasons);
        Assert.Null(record.Reason);
        var alert = Assert.Single(metrics.Events, entry => entry.Name == "audit.acts.nonconforming");
        Assert.Equal("identidade", alert.Tags["origin"]);
        Assert.Equal("papel-revogado", alert.Tags["type"]);
        Assert.DoesNotContain("reasons", alert.Tags.Keys);
        var warning = Assert.Single(logs.Events, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("motivo-ausente", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Motivo confidencial", warning.Message, StringComparison.Ordinal);
        await AssertWasAcknowledgedAsync(cancellationToken);
    }

    [Fact(DisplayName = nameof(RecordsMissingAuthorAndTargetWithBothReasonsAndOneAlert))]
    public async Task RecordsMissingAuthorAndTargetWithBothReasonsAndOneAlert()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("papel-concedido") with { Autor = null, Alvo = null };
        using var metrics = new ActMetricCapture();
        using var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logs, cancellationToken);

        await PublishAsync(act, cancellationToken);
        var record = await WaitForRecordAsync(host, act.FatoId, cancellationToken);
        await metrics.WaitForCountAsync("audit.acts.nonconforming", 1, cancellationToken);
        await logs.WaitForWarningAsync(act.FatoId, cancellationToken);

        Assert.Null(record.AuthorId);
        Assert.Null(record.AuthorType);
        Assert.Null(record.TargetId);
        Assert.Null(record.TargetType);
        Assert.Equal(new[] { "autor-ausente", "alvo-ausente" }, record.Reasons);
        Assert.Single(metrics.Events, entry => entry.Name == "audit.acts.nonconforming");
        Assert.Single(logs.Events, entry => entry.Level == LogLevel.Warning);
        await AssertWasAcknowledgedAsync(cancellationToken);
    }

    [Fact(DisplayName = nameof(RecordsUnknownTypeAndPreservesReceivedText))]
    public async Task RecordsUnknownTypeAndPreservesReceivedText()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("papel-alterado");
        using var metrics = new ActMetricCapture();
        using var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logs, cancellationToken);

        await PublishAsync(act, cancellationToken);
        var record = await WaitForRecordAsync(host, act.FatoId, cancellationToken);
        await metrics.WaitForCountAsync("audit.acts.nonconforming", 1, cancellationToken);
        await logs.WaitForWarningAsync(act.FatoId, cancellationToken);

        Assert.Equal("papel-alterado", record.Type);
        Assert.Equal(new[] { "tipo-desconhecido" }, record.Reasons);
        Assert.Single(metrics.Events, entry => entry.Name == "audit.acts.nonconforming");
        Assert.Single(logs.Events, entry => entry.Level == LogLevel.Warning);
        await AssertWasAcknowledgedAsync(cancellationToken);
    }

    [Fact(DisplayName = nameof(AcknowledgesNonConformingRedeliveryWithoutRepeatingAlert))]
    public async Task AcknowledgesNonConformingRedeliveryWithoutRepeatingAlert()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("papel-revogado") with { Motivo = null };
        using var metrics = new ActMetricCapture();
        using var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logs, cancellationToken);

        await PublishAsync(act, cancellationToken);
        await WaitForRecordAsync(host, act.FatoId, cancellationToken);
        await metrics.WaitForCountAsync("audit.acts.nonconforming", 1, cancellationToken);

        await PublishAsync(act, cancellationToken);
        await metrics.WaitForCountAsync("audit.acts.redelivered", 1, cancellationToken);
        await AssertWasAcknowledgedAsync(cancellationToken);

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var records = await dbContext.AuditRecords
            .AsNoTracking()
            .CountAsync(record => record.FactId == act.FatoId, cancellationToken);
        Assert.Equal(1, records);
        Assert.Single(metrics.Events, entry => entry.Name == "audit.acts.nonconforming");
        Assert.Single(logs.Events, entry => entry.Level == LogLevel.Warning);
    }

    private AtoPraticado CreateAct(string type)
        => new()
        {
            FatoId = Guid.CreateVersion7(),
            Origem = "identidade",
            Tipo = type,
            TenantId = Guid.CreateVersion7(),
            PraticadoEm = new DateTimeOffset(2026, 10, 2, 14, 5, 11, TimeSpan.Zero),
            Autor = new ReferenciaAto { Tipo = "conta-interna", Id = Guid.CreateVersion7() },
            Alvo = new ReferenciaAto { Tipo = "conta-interna", Id = Guid.CreateVersion7() },
            Complemento = new Dictionary<string, string> { ["papel"] = "professor" },
            Motivo = "Motivo confidencial",
        };

    private async Task<IHost> StartHostAsync(
        ILoggerProvider loggerProvider,
        CancellationToken cancellationToken)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = Exchange,
            ["RabbitMq:DeadLetterExchange"] = "audit.integration.events.dlx",
            ["RabbitMq:AuditQueue"] = Queue,
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
                logging.AddProvider(loggerProvider);
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

    private async Task PublishAsync(AtoPraticado act, CancellationToken cancellationToken)
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
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(act, SerializerOptions));
        await channel.BasicPublishAsync(Exchange, RoutingKey, true, properties, body, cancellationToken);
    }

    private static async Task<AuditRecord> WaitForRecordAsync(
        IHost host,
        Guid factId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var record = await dbContext.AuditRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.FactId == factId, cancellationToken);
            if (record is not null)
            {
                return record;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("The non-conforming administrative act was not recorded.");
    }

    private async Task AssertWasAcknowledgedAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await GetQueueMessageCountAsync(Queue, cancellationToken) is 0
                && await GetQueueMessageCountAsync(DeadLetterQueue, cancellationToken) is 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("A non-conforming act remained queued or reached the dead-letter queue.");
    }

    private async Task<uint> GetQueueMessageCountAsync(string queue, CancellationToken cancellationToken)
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
        var queueInfo = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
        return queueInfo.MessageCount;
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

        public async Task WaitForWarningAsync(Guid factId, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (Events.Any(entry => entry.Level == LogLevel.Warning
                    && entry.Message.Contains(factId.ToString(), StringComparison.Ordinal)))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }

            throw new TimeoutException("The non-conforming administrative act warning was not logged.");
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
                events.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
            }
        }
    }

    private sealed class ActMetricCapture : IDisposable
    {
        private readonly ConcurrentQueue<MetricEvent> events = new();
        private readonly MeterListener listener = new();

        public IReadOnlyCollection<MetricEvent> Events => events.ToArray();

        public ActMetricCapture()
        {
            listener.InstrumentPublished = static (instrument, measurementListener) =>
            {
                if (instrument.Meter.Name == AuditTelemetry.MeterName
                    && instrument.Name is "audit.acts.nonconforming" or "audit.acts.redelivered")
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

        public async Task WaitForCountAsync(
            string instrumentName,
            int expectedCount,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (Events.Count(entry => entry.Name == instrumentName) >= expectedCount)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }

            throw new TimeoutException($"Expected metric '{instrumentName}' was not recorded.");
        }

        public void Dispose() => listener.Dispose();
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed record MetricEvent(string Name, long Value, IReadOnlyDictionary<string, string?> Tags);
}
