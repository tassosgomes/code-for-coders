using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
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
public sealed class AdministrativeActRedeliveryTests(AuditIntegrationFixture fixture)
{
    private const string Exchange = "audit.integration.events";
    private const string RoutingKey = "auditoria.ato-praticado.v1";
    private const string Queue = "audit.integration.acts";
    private const string DeadLetterQueue = "audit.integration.acts.dlq";
    private const string Origin = "identidade";
    private static readonly DateTimeOffset ReceivedOn = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact(DisplayName = nameof(AcknowledgesIdenticalRedeliveryWithoutCreatingARecordOrWarning))]
    public async Task AcknowledgesIdenticalRedeliveryWithoutCreatingARecordOrWarning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct();
        using var metrics = new RedeliveryMetricCapture();
        using var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(fixture, logs, cancellationToken);

        await PublishAsync(act, cancellationToken);
        await WaitForRecordCountAsync(act.FatoId, 1, cancellationToken);
        await PublishAsync(act, cancellationToken);
        await metrics.WaitForOutcomeCountAsync("identical", 1, cancellationToken);

        Assert.Equal(1, await GetRecordCountAsync(act.FatoId, cancellationToken));
        Assert.DoesNotContain(logs.Events, entry => entry.Level == LogLevel.Warning);
        Assert.Equal(0u, await GetQueueMessageCountAsync(DeadLetterQueue, cancellationToken));
    }

    [Fact(DisplayName = nameof(RecordsSameContentWhenFactIdIsDifferent))]
    public async Task RecordsSameContentWhenFactIdIsDifferent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstAct = CreateAct();
        var secondAct = firstAct with { FatoId = Guid.CreateVersion7() };
        using var metrics = new RedeliveryMetricCapture();
        using var host = await StartHostAsync(fixture, null, cancellationToken);

        await PublishAsync(firstAct, cancellationToken);
        await PublishAsync(secondAct, cancellationToken);
        await WaitForRecordCountAsync(firstAct.FatoId, 1, cancellationToken);
        await WaitForRecordCountAsync(secondAct.FatoId, 1, cancellationToken);

        var firstFingerprint = await ReadFingerprintAsync(firstAct.FatoId, cancellationToken);
        var secondFingerprint = await ReadFingerprintAsync(secondAct.FatoId, cancellationToken);
        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.Empty(metrics.Outcomes);
        Assert.Equal(0u, await GetQueueMessageCountAsync(DeadLetterQueue, cancellationToken));
    }

    [Fact(DisplayName = nameof(AcknowledgesDivergentRedeliveryAndPreservesTheOriginalRecord))]
    public async Task AcknowledgesDivergentRedeliveryAndPreservesTheOriginalRecord()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string originalReason = "Confidential original business reason";
        const string receivedReason = "Confidential changed business reason";
        const string originalComplement = "original-private-value";
        const string receivedComplement = "changed-private-value";
        var originalAct = CreateAct() with
        {
            Motivo = originalReason,
            Complemento = new Dictionary<string, string> { ["papel"] = originalComplement },
        };
        var changedAct = originalAct with
        {
            Motivo = receivedReason,
            Complemento = new Dictionary<string, string> { ["papel"] = receivedComplement },
        };
        using var metrics = new RedeliveryMetricCapture();
        using var logs = new CapturingLoggerProvider();
        using var host = await StartHostAsync(fixture, logs, cancellationToken);

        await PublishAsync(originalAct, cancellationToken);
        await WaitForRecordCountAsync(originalAct.FatoId, 1, cancellationToken);
        var originalSnapshot = await ReadRecordSnapshotAsync(originalAct.FatoId, cancellationToken);
        var originalFingerprint = await ReadFingerprintAsync(originalAct.FatoId, cancellationToken);

        await PublishAsync(changedAct, cancellationToken);
        await metrics.WaitForOutcomeCountAsync("divergent", 1, cancellationToken);
        await logs.WaitForWarningAsync(originalAct.FatoId, cancellationToken);

        var warning = Assert.Single(logs.Events, entry => entry.Level == LogLevel.Warning);
        var afterSnapshot = await ReadRecordSnapshotAsync(originalAct.FatoId, cancellationToken);
        var warningFingerprints = System.Text.RegularExpressions.Regex
            .Matches(warning.Message, "\\b[0-9a-f]{64}\\b")
            .Select(match => match.Value)
            .ToArray();

        Assert.Equal(originalSnapshot, afterSnapshot);
        Assert.Equal(1, await GetRecordCountAsync(originalAct.FatoId, cancellationToken));
        Assert.Contains(Origin, warning.Message, StringComparison.Ordinal);
        Assert.Contains(originalAct.FatoId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Contains(originalFingerprint, warningFingerprints);
        Assert.Equal(2, warningFingerprints.Length);
        Assert.NotEqual(warningFingerprints[0], warningFingerprints[1]);
        Assert.DoesNotContain(originalReason, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(receivedReason, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(originalComplement, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(receivedComplement, warning.Message, StringComparison.Ordinal);
        Assert.Equal(0u, await GetQueueMessageCountAsync(DeadLetterQueue, cancellationToken));
    }

    [Fact(DisplayName = nameof(ConcurrentRedeliveriesCreateOneRecordAndDoNotReachTheDeadLetterQueue))]
    public async Task ConcurrentRedeliveriesCreateOneRecordAndDoNotReachTheDeadLetterQueue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct();
        using var metrics = new RedeliveryMetricCapture();
        using var firstHost = await StartHostAsync(fixture, null, cancellationToken);
        using var secondHost = await StartHostAsync(fixture, null, cancellationToken);
        await WaitForConsumerCountAsync(2, cancellationToken);

        await using var lockConnection = new NpgsqlConnection(fixture.PostgreSql.GetConnectionString());
        await lockConnection.OpenAsync(cancellationToken);
        await using var transaction = await lockConnection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await using (var lockCommand = lockConnection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "LOCK TABLE audit_access.audit_records IN SHARE MODE";
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await Task.WhenAll(
            PublishAsync(act, cancellationToken),
            PublishAsync(act, cancellationToken));
        await WaitForBlockedInsertCountAsync(2, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await metrics.WaitForOutcomeCountAsync("identical", 1, cancellationToken);

        Assert.Equal(1, await GetRecordCountAsync(act.FatoId, cancellationToken));
        Assert.Equal(0u, await GetQueueMessageCountAsync(DeadLetterQueue, cancellationToken));
    }

    private static AtoPraticado CreateAct()
        => new()
        {
            FatoId = Guid.CreateVersion7(),
            Origem = Origin,
            Tipo = "papel-concedido",
            TenantId = Guid.CreateVersion7(),
            PraticadoEm = new DateTimeOffset(2026, 10, 2, 14, 5, 11, TimeSpan.Zero),
            Autor = new ReferenciaAto
            {
                Tipo = "conta-interna",
                Id = Guid.CreateVersion7(),
            },
            Alvo = new ReferenciaAto
            {
                Tipo = "conta-interna",
                Id = Guid.CreateVersion7(),
            },
            Complemento = new Dictionary<string, string> { ["papel"] = "professor" },
            Motivo = "Administrative reason",
        };

    private static async Task<IHost> StartHostAsync(
        AuditIntegrationFixture fixture,
        ILoggerProvider? loggerProvider,
        CancellationToken cancellationToken)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.RuntimeConnectionString,
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
        await channel.BasicPublishAsync(
            Exchange,
            RoutingKey,
            mandatory: true,
            properties,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(act, SerializerOptions)),
            cancellationToken);
    }

    private async Task WaitForRecordCountAsync(
        Guid factId,
        long expectedCount,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (await GetRecordCountAsync(factId, cancellationToken) == expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("The expected administrative act record count was not reached.");
    }

    private async Task<long> GetRecordCountAsync(Guid factId, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(fixture);
        return await dbContext.AuditRecords
            .AsNoTracking()
            .LongCountAsync(record => record.FactId == factId, cancellationToken);
    }

    private async Task<string> ReadFingerprintAsync(Guid factId, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(fixture);
        var fingerprint = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.FactId == factId)
            .Select(record => record.Fingerprint)
            .SingleAsync(cancellationToken);
        return fingerprint;
    }

    private async Task<string> ReadRecordSnapshotAsync(Guid factId, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(fixture);
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT to_jsonb(record)::text
            FROM audit_access.audit_records AS record
            WHERE record.origem = @origin AND record.fato_id = @fact_id
            """;
        AddParameter(command, "origin", Origin);
        AddParameter(command, "fact_id", factId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The administrative act record was not found.");
    }

    private async Task<uint> GetQueueMessageCountAsync(string queue, CancellationToken cancellationToken)
    {
        await using var connection = await CreateRabbitConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var queueInfo = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
        return queueInfo.MessageCount;
    }

    private async Task WaitForConsumerCountAsync(int expectedCount, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var connection = await CreateRabbitConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            var queueInfo = await channel.QueueDeclarePassiveAsync(Queue, cancellationToken);
            if (queueInfo.ConsumerCount >= expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The expected number of RabbitMQ consumers did not start.");
    }

    private async Task WaitForBlockedInsertCountAsync(int expectedCount, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgreSql.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT count(*)
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND wait_event_type = 'Lock'
                  AND query ILIKE 'INSERT INTO audit_access.audit_records%'
                  AND pid <> pg_backend_pid()
                """;
            var blockedCount = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
            if (blockedCount >= expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("Both duplicate inserts did not reach the database concurrently.");
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

    private static AuditDbContext CreateDbContext(AuditIntegrationFixture fixture)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        return new AuditDbContext(options);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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

            throw new TimeoutException("The divergent administrative act warning was not logged.");
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
            listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            {
                string? outcome = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome")
                    {
                        outcome = tag.Value?.ToString();
                        break;
                    }
                }

                if (outcome is not null)
                {
                    outcomes.Enqueue(outcome);
                }
            });
            listener.Start();
        }

        public async Task WaitForOutcomeCountAsync(
            string outcome,
            int expectedCount,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (Outcomes.Count(value => value == outcome) >= expectedCount)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }

            throw new TimeoutException($"Expected redelivery outcome '{outcome}' was not recorded.");
        }

        public void Dispose() => listener.Dispose();
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
