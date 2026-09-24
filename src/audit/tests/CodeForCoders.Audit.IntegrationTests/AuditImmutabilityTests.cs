using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using CodeForCoders.Audit.Application;
using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.ValueObjects;
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
public sealed class AuditImmutabilityTests(AuditIntegrationFixture fixture)
{
    private const string Exchange = "audit.integration.immutability.events";
    private const string DeadLetterExchange = "audit.integration.immutability.events.dlx";
    private const string Queue = "audit.integration.immutability.acts";
    private const string RoutingKey = "auditoria.ato-praticado.v1";
    private static readonly DateTimeOffset ReceivedOn = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact(DisplayName = nameof(RuntimeCredentialCanAppendAuditRecords))]
    public async Task RuntimeCredentialCanAppendAuditRecords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct();
        using var host = await StartHostAsync(cancellationToken);
        await WaitForConsumerCountAsync(1, cancellationToken);

        await PublishAsync(act, cancellationToken);
        var record = await WaitForRecordAsync(act.FatoId, cancellationToken);

        Assert.Equal(act.FatoId, record.FactId);
        Assert.Equal(act.Origem, record.Origin);
        Assert.Equal("conforming", record.Conformity);
    }

    [Fact(DisplayName = nameof(RuntimeCredentialCannotUpdateAuditRecords))]
    public async Task RuntimeCredentialCannotUpdateAuditRecords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var record = await SeedRecordAsync(cancellationToken);
        var before = await ReadSnapshotAsync(record.FactId, cancellationToken);

        var exception = await ExecuteMutationAsync(
            fixture.RuntimeConnectionString,
            "UPDATE audit_access.audit_records SET conformidade = 'tampered' WHERE fato_id = @factId",
            record.FactId,
            cancellationToken);

        Assert.Equal("42501", exception.SqlState);
        Assert.Equal(before, await ReadSnapshotAsync(record.FactId, cancellationToken));
    }

    [Fact(DisplayName = nameof(RuntimeCredentialCannotDeleteAuditRecords))]
    public async Task RuntimeCredentialCannotDeleteAuditRecords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var record = await SeedRecordAsync(cancellationToken);
        var before = await ReadSnapshotAsync(record.FactId, cancellationToken);

        var exception = await ExecuteMutationAsync(
            fixture.RuntimeConnectionString,
            "DELETE FROM audit_access.audit_records WHERE fato_id = @factId",
            record.FactId,
            cancellationToken);

        Assert.Equal("42501", exception.SqlState);
        Assert.Equal(before, await ReadSnapshotAsync(record.FactId, cancellationToken));
    }

    [Fact(DisplayName = nameof(RuntimeCredentialCannotTruncateAuditRecords))]
    public async Task RuntimeCredentialCannotTruncateAuditRecords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var record = await SeedRecordAsync(cancellationToken);
        var before = await ReadSnapshotAsync(record.FactId, cancellationToken);

        var exception = await ExecuteMutationAsync(
            fixture.RuntimeConnectionString,
            "TRUNCATE TABLE audit_access.audit_records",
            record.FactId,
            cancellationToken);

        Assert.Equal("42501", exception.SqlState);
        Assert.Equal(before, await ReadSnapshotAsync(record.FactId, cancellationToken));
    }

    [Fact(DisplayName = nameof(DatabaseOwnerTriggerRejectsUpdate))]
    public async Task DatabaseOwnerTriggerRejectsUpdate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var record = await SeedRecordAsync(cancellationToken);
        var before = await ReadSnapshotAsync(record.FactId, cancellationToken);

        var exception = await ExecuteMutationAsync(
            fixture.MigrationConnectionString,
            "UPDATE audit_access.audit_records SET conformidade = 'tampered' WHERE fato_id = @factId",
            record.FactId,
            cancellationToken);

        Assert.Equal("P0001", exception.SqlState);
        Assert.Contains("append-only", exception.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await ReadSnapshotAsync(record.FactId, cancellationToken));
    }

    [Fact(DisplayName = nameof(DatabaseOwnerTriggerRejectsDelete))]
    public async Task DatabaseOwnerTriggerRejectsDelete()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var record = await SeedRecordAsync(cancellationToken);
        var before = await ReadSnapshotAsync(record.FactId, cancellationToken);

        var exception = await ExecuteMutationAsync(
            fixture.MigrationConnectionString,
            "DELETE FROM audit_access.audit_records WHERE fato_id = @factId",
            record.FactId,
            cancellationToken);

        Assert.Equal("P0001", exception.SqlState);
        Assert.Contains("append-only", exception.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await ReadSnapshotAsync(record.FactId, cancellationToken));
    }

    [Fact(DisplayName = nameof(ProducerServiceRolesCannotConnectToTheAuditDatabase))]
    public async Task ProducerServiceRolesCannotConnectToTheAuditDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(fixture.MigrationConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT role_name, has_database_privilege(role_name, current_database(), 'CONNECT')
            FROM unnest(@producerRoles::text[]) AS producer(role_name)
            ORDER BY role_name
            """;
        command.Parameters.AddWithValue("producerRoles", AuditIntegrationFixture.ProducerRoleNames);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var checkedRoles = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            checkedRoles++;
            Assert.False(reader.GetBoolean(1), $"Role {reader.GetString(0)} can connect to the audit database.");
        }

        Assert.Equal(AuditIntegrationFixture.ProducerRoleNames.Length, checkedRoles);
    }

    [Fact(DisplayName = nameof(RestartAndIdenticalRedeliveryPreserveRecordContent))]
    public async Task RestartAndIdenticalRedeliveryPreserveRecordContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct();
        using var redeliveries = new RedeliveryMetricCapture();
        string originalSnapshot;

        using (var firstHost = await StartHostAsync(cancellationToken))
        {
            await WaitForConsumerCountAsync(1, cancellationToken);
            await PublishAsync(act, cancellationToken);
            await WaitForRecordAsync(act.FatoId, cancellationToken);
            originalSnapshot = await ReadSnapshotAsync(act.FatoId, cancellationToken);
        }

        using (var restartedHost = await StartHostAsync(cancellationToken))
        {
            await WaitForConsumerCountAsync(1, cancellationToken);
            await PublishAsync(act, cancellationToken);
            await redeliveries.WaitForIdenticalOutcomeAsync(cancellationToken);
        }

        Assert.Equal(1, await ReadRecordCountAsync(act.FatoId, cancellationToken));
        Assert.Equal(originalSnapshot, await ReadSnapshotAsync(act.FatoId, cancellationToken));
    }

    private async Task<AuditRecord> SeedRecordAsync(CancellationToken cancellationToken)
    {
        var record = NewRecord();
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.MigrationConnectionString)
            .Options;
        await using var dbContext = new AuditDbContext(options);
        dbContext.AuditRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    private async Task<string> ReadSnapshotAsync(Guid factId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT to_jsonb(record)::text
            FROM audit_access.audit_records AS record
            WHERE fato_id = @factId
            """;
        command.Parameters.AddWithValue("factId", factId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The seeded audit record was not found.");
    }

    private async Task<long> ReadRecordCountAsync(Guid factId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.MigrationConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM audit_access.audit_records WHERE fato_id = @factId";
        command.Parameters.AddWithValue("factId", factId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<AuditRecord> WaitForRecordAsync(Guid factId, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.RuntimeConnectionString)
            .Options;
        for (var attempt = 0; attempt < 150; attempt++)
        {
            await using var dbContext = new AuditDbContext(options);
            var record = await dbContext.AuditRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.FactId == factId, cancellationToken);
            if (record is not null)
            {
                return record;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The audit record was not written using the runtime credential.");
    }

    private async Task<PostgresException> ExecuteMutationAsync(
        string connectionString,
        string sql,
        Guid factId,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        if (sql.Contains("@factId", StringComparison.Ordinal))
        {
            command.Parameters.AddWithValue("factId", factId);
        }

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(cancellationToken));
        await transaction.RollbackAsync(cancellationToken);
        return exception;
    }

    private async Task<IHost> StartHostAsync(CancellationToken cancellationToken)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.RuntimeConnectionString,
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = Exchange,
            ["RabbitMq:DeadLetterExchange"] = DeadLetterExchange,
            ["RabbitMq:AuditQueue"] = Queue,
            ["RabbitMq:EventRoutingKey"] = RoutingKey,
            ["AuditDatabase:WriterRole"] = AuditIntegrationFixture.WriterRole,
        };

        var host = Host.CreateDefaultBuilder()
            .UseEnvironment("IntegrationTest")
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(configurationValues))
            .ConfigureLogging(logging => logging.ClearProviders())
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

        throw new TimeoutException("The audit runtime consumer did not start.");
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

    private static AtoPraticado CreateAct()
        => new()
        {
            FatoId = Guid.CreateVersion7(),
            Origem = "identidade",
            Tipo = "papel-concedido",
            TenantId = Guid.CreateVersion7(),
            PraticadoEm = new DateTimeOffset(2026, 10, 2, 14, 5, 11, TimeSpan.Zero),
            Autor = new ReferenciaAto { Tipo = "conta-interna", Id = Guid.CreateVersion7() },
            Alvo = new ReferenciaAto { Tipo = "conta-interna", Id = Guid.CreateVersion7() },
            Complemento = new Dictionary<string, string> { ["papel"] = "professor" },
            Motivo = "Administrative reason",
        };

    private static AuditRecord NewRecord()
        => AuditRecord.Create(
            new AdministrativeAct(
                Guid.CreateVersion7(),
                "identidade",
                "papel-concedido",
                Guid.CreateVersion7(),
                new DateTimeOffset(2026, 10, 2, 14, 5, 11, TimeSpan.Zero),
                new AdministrativeActReference("conta-interna", Guid.CreateVersion7()),
                new AdministrativeActReference("conta-interna", Guid.CreateVersion7()),
                new Dictionary<string, string> { ["papel"] = "professor" },
                "Administrative reason"),
            ReceivedOn);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RedeliveryMetricCapture : IDisposable
    {
        private readonly TaskCompletionSource identicalOutcomeObserved = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly MeterListener listener = new();

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
                if (instrument.Name != "audit.acts.redelivered")
                {
                    return;
                }

                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome" && tag.Value?.ToString() == "identical")
                    {
                        identicalOutcomeObserved.TrySetResult();
                        return;
                    }
                }
            });
            listener.Start();
        }

        public Task WaitForIdenticalOutcomeAsync(CancellationToken cancellationToken)
            => identicalOutcomeObserved.Task.WaitAsync(cancellationToken);

        public void Dispose() => listener.Dispose();
    }
}
