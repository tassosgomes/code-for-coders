using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
public sealed class AdministrativeActRecordingTests(AuditIntegrationFixture fixture)
{
    private const string Exchange = "audit.integration.events";
    private const string RoutingKey = "auditoria.ato-praticado.v1";
    private const string EmailSentinel = "person@example.invalid";
    private const string NameSentinel = "Unexpected Personal Name";
    private const string ReasonSentinel = "Confidential business reason";
    private static readonly DateTimeOffset ReceivedOn = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact(DisplayName = nameof(RecordsInternalInvitationIssued))]
    public async Task RecordsInternalInvitationIssued()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("convite-interno-emitido");
        using var host = await StartHostAsync(fixture, ReceivedOn, null, cancellationToken);

        await PublishAsync(act, cancellationToken);
        var record = await WaitForRecordAsync(host, act.FatoId, cancellationToken);

        AssertConformingRecord(act, record);
    }

    [Fact(DisplayName = nameof(RecordsInternalInvitationAcceptedWithoutReason))]
    public async Task RecordsInternalInvitationAcceptedWithoutReason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("convite-interno-aceito");
        using var host = await StartHostAsync(fixture, ReceivedOn, null, cancellationToken);

        await PublishAsync(act, cancellationToken);
        var record = await WaitForRecordAsync(host, act.FatoId, cancellationToken);

        AssertConformingRecord(act, record);
        Assert.Null(record.Reason);
        Assert.Null(record.Complement);
    }

    [Fact(DisplayName = nameof(RecordsRoleGranted))]
    public async Task RecordsRoleGranted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("papel-concedido");
        using var host = await StartHostAsync(fixture, ReceivedOn, null, cancellationToken);

        await PublishAsync(act, cancellationToken);
        var record = await WaitForRecordAsync(host, act.FatoId, cancellationToken);

        AssertConformingRecord(act, record);
    }

    [Fact(DisplayName = nameof(RecordsRoleRevoked))]
    public async Task RecordsRoleRevoked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("papel-revogado");
        using var host = await StartHostAsync(fixture, ReceivedOn, null, cancellationToken);

        await PublishAsync(act, cancellationToken);
        var record = await WaitForRecordAsync(host, act.FatoId, cancellationToken);

        AssertConformingRecord(act, record);
    }

    [Fact(DisplayName = nameof(RecordsEachActInItsOwnTenant))]
    public async Task RecordsEachActInItsOwnTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstTenant = Guid.CreateVersion7();
        var secondTenant = Guid.CreateVersion7();
        var firstAct = CreateAct("papel-concedido", firstTenant);
        var secondAct = CreateAct("papel-revogado", secondTenant);
        using var host = await StartHostAsync(fixture, ReceivedOn, null, cancellationToken);

        await PublishAsync(firstAct, cancellationToken);
        await PublishAsync(secondAct, cancellationToken);
        var firstRecord = await WaitForRecordAsync(host, firstAct.FatoId, cancellationToken);
        var secondRecord = await WaitForRecordAsync(host, secondAct.FatoId, cancellationToken);

        Assert.Equal(firstTenant, firstRecord.TenantId);
        Assert.Equal(secondTenant, secondRecord.TenantId);
        Assert.NotEqual(firstRecord.TenantId, secondRecord.TenantId);
    }

    [Fact(DisplayName = nameof(FreshMigrationRebuildsTableAndProtectsRecords))]
    public async Task FreshMigrationRebuildsTableAndProtectsRecords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new AuditDbContext(options);
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        var rebuiltColumnCount = await GetCountAsync(
            connection,
            """
            SELECT count(*)::bigint
            FROM information_schema.columns
            WHERE table_schema = 'audit_access'
              AND table_name = 'audit_records'
              AND column_name = ANY (ARRAY[
                  'origem', 'fato_id', 'tipo', 'autor_tipo', 'autor_id', 'alvo_tipo', 'alvo_id',
                  'complemento', 'motivo', 'praticado_em', 'recebido_em', 'conformidade',
                  'razoes', 'impressao_digital'
              ])
            """,
            cancellationToken);
        var legacyColumnCount = await GetCountAsync(
            connection,
            """
            SELECT count(*)::bigint
            FROM information_schema.columns
            WHERE table_schema = 'audit_access'
              AND table_name = 'audit_records'
              AND column_name = ANY (ARRAY[
                  'source_service', 'event_type', 'payload', 'occurred_on', 'recorded_on'
              ])
            """,
            cancellationToken);
        var uniqueIndexCount = await GetCountAsync(
            connection,
            """
            SELECT count(*)::bigint
            FROM pg_indexes
            WHERE schemaname = 'audit_access'
              AND tablename = 'audit_records'
              AND indexname = 'ux_audit_records_origem_fato_id'
              AND position('(origem, fato_id)' in indexdef) > 0
            """,
            cancellationToken);
        var tenantIndexCount = await GetCountAsync(
            connection,
            """
            SELECT count(*)::bigint
            FROM pg_indexes
            WHERE schemaname = 'audit_access'
              AND tablename = 'audit_records'
              AND indexname = 'ix_audit_records_tenant_praticado_em'
              AND position('(tenant_id, praticado_em)' in indexdef) > 0
            """,
            cancellationToken);
        var triggerCount = await GetCountAsync(
            connection,
            """
            SELECT count(*)::bigint
            FROM pg_trigger AS tg
            JOIN pg_class table_definition ON table_definition.oid = tg.tgrelid
            JOIN pg_namespace schema_definition ON schema_definition.oid = table_definition.relnamespace
            WHERE schema_definition.nspname = 'audit_access'
              AND table_definition.relname = 'audit_records'
              AND tg.tgname = 'audit_records_append_only'
              AND tg.tgenabled = 'O'
              AND NOT tg.tgisinternal
            """,
            cancellationToken);
        var functionCount = await GetCountAsync(
            connection,
            """
            SELECT CASE
                WHEN to_regprocedure('audit_access.prevent_audit_record_mutation()') IS NULL THEN 0::bigint
                ELSE 1::bigint
            END
            """,
            cancellationToken);

        Assert.Equal(14L, rebuiltColumnCount);
        Assert.Equal(0L, legacyColumnCount);
        Assert.Equal(1L, uniqueIndexCount);
        Assert.Equal(1L, tenantIndexCount);
        Assert.Equal(1L, triggerCount);
        Assert.Equal(1L, functionCount);
    }

    [Fact(DisplayName = nameof(DiscardsFieldsOutsideTheContractAndKeepsTelemetryPrivate))]
    public async Task DiscardsFieldsOutsideTheContractAndKeepsTelemetryPrivate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var act = CreateAct("papel-concedido", reason: ReasonSentinel);
        using var logs = new CapturingLoggerProvider();
        using var activities = new ActivityCapture();
        using var host = await StartHostAsync(fixture, ReceivedOn, logs, cancellationToken);

        var json = JsonSerializer.SerializeToNode(act, SerializerOptions)!.AsObject();
        json["email"] = EmailSentinel;
        json["nome"] = NameSentinel;
        await PublishAsync(json.ToJsonString(SerializerOptions), cancellationToken);

        var record = await WaitForRecordAsync(host, act.FatoId, cancellationToken);
        await WaitForLogAsync(logs, act.FatoId, cancellationToken);
        var serializedRecord = JsonSerializer.Serialize(record, SerializerOptions);
        Assert.DoesNotContain(EmailSentinel, serializedRecord, StringComparison.Ordinal);
        Assert.DoesNotContain(NameSentinel, serializedRecord, StringComparison.Ordinal);

        var logText = string.Join(Environment.NewLine, logs.Messages);
        Assert.DoesNotContain(EmailSentinel, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(NameSentinel, logText, StringComparison.Ordinal);
        Assert.DoesNotContain(ReasonSentinel, logText, StringComparison.Ordinal);

        var completedActivities = await WaitForActActivitiesAsync(activities, cancellationToken);
        Assert.Equal(2, completedActivities.Length);
        foreach (var activity in completedActivities)
        {
            Assert.Equal(
                new[] { "fatoId", "origem", "tenantId", "tipo" },
                activity.TagObjects.Select(tag => tag.Key).Order(StringComparer.Ordinal));
            Assert.Equal(act.FatoId, activity.GetTagItem("fatoId"));
            Assert.Equal(act.Origem, activity.GetTagItem("origem"));
            Assert.Equal(act.Tipo, activity.GetTagItem("tipo"));
            Assert.Equal(act.TenantId, activity.GetTagItem("tenantId"));
        }
    }

    private static AtoPraticado CreateAct(
        string type,
        Guid? tenantId = null,
        string? reason = null)
    {
        var requiresReason = type != "convite-interno-aceito";
        return new AtoPraticado
        {
            FatoId = Guid.CreateVersion7(),
            Origem = "identidade",
            Tipo = type,
            TenantId = tenantId ?? Guid.CreateVersion7(),
            PraticadoEm = new DateTimeOffset(2026, 10, 2, 14, 5, 11, TimeSpan.Zero),
            Autor = new ReferenciaAto
            {
                Tipo = "conta-interna",
                Id = Guid.CreateVersion7(),
            },
            Alvo = new ReferenciaAto
            {
                Tipo = type.StartsWith("convite-interno", StringComparison.Ordinal)
                    ? "convite-interno"
                    : "conta-interna",
                Id = Guid.CreateVersion7(),
            },
            Complemento = type == "convite-interno-aceito"
                ? null
                : new Dictionary<string, string> { ["papel"] = "professor" },
            Motivo = requiresReason ? reason ?? "Administrative reason" : null,
        };
    }

    private static void AssertConformingRecord(AtoPraticado act, AuditRecord record)
    {
        Assert.Equal(act.TenantId, record.TenantId);
        Assert.Equal(act.Origem, record.Origin);
        Assert.Equal(act.FatoId, record.FactId);
        Assert.Equal(act.Tipo, record.Type);
        Assert.Equal(act.Autor!.Tipo, record.AuthorType);
        Assert.Equal(act.Autor.Id, record.AuthorId);
        Assert.Equal(act.Alvo!.Tipo, record.TargetType);
        Assert.Equal(act.Alvo.Id, record.TargetId);
        Assert.Equal(act.Motivo, record.Reason);
        Assert.Equal(act.PraticadoEm!.Value.ToUniversalTime(), record.PracticedOn);
        Assert.Equal(ReceivedOn, record.ReceivedOn);
        Assert.Equal("conforming", record.Conformity);
        Assert.Empty(record.Reasons);
        Assert.Equal(64, record.Fingerprint.Length);

        if (act.Complemento is null)
        {
            Assert.Null(record.Complement);
        }
        else
        {
            var complement = JsonSerializer.Deserialize<Dictionary<string, string>>(record.Complement!);
            Assert.Equal(act.Complemento, complement);
        }
    }

    private static async Task<IHost> StartHostAsync(
        AuditIntegrationFixture fixture,
        DateTimeOffset receivedOn,
        ILoggerProvider? loggerProvider,
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
            ["RabbitMq:AuditQueue"] = "audit.integration.acts",
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
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(receivedOn));
            })
            .Build();

        await host.StartAsync(cancellationToken);
        return host;
    }

    private async Task PublishAsync(AtoPraticado act, CancellationToken cancellationToken)
        => await PublishAsync(JsonSerializer.Serialize(act, SerializerOptions), cancellationToken);

    private async Task PublishAsync(string json, CancellationToken cancellationToken)
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
            Encoding.UTF8.GetBytes(json),
            cancellationToken);
    }

    private async Task<AuditRecord> WaitForRecordAsync(
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

        throw new TimeoutException("The administrative act was not recorded.");
    }

    private static async Task<long> GetCountAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static async Task WaitForLogAsync(
        CapturingLoggerProvider loggerProvider,
        Guid factId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (loggerProvider.Messages.Any(message => message.Contains(factId.ToString(), StringComparison.Ordinal)))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The administrative act was not logged.");
    }

    private static async Task<Activity[]> WaitForActActivitiesAsync(
        ActivityCapture activityCapture,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var activities = activityCapture.Activities
                .Where(activity => activity.OperationName is "audit.acts.consume" or "audit.acts.recorded")
                .ToArray();
            if (activities.Length == 2)
            {
                return activities;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        return activityCapture.Activities
            .Where(activity => activity.OperationName is "audit.acts.consume" or "audit.acts.recorded")
            .ToArray();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();

        public IEnumerable<string> Messages => messages;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
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
                messages.Enqueue(formatter(state, exception));
            }
        }
    }

    private sealed class ActivityCapture : IDisposable
    {
        private readonly ConcurrentQueue<Activity> activities = new();
        private readonly ActivityListener listener;

        public ActivityCapture()
        {
            listener = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name is AuditTelemetry.ActivitySourceName or RabbitMqTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => activities.Enqueue(activity),
            };
            ActivitySource.AddActivityListener(listener);
        }

        public IEnumerable<Activity> Activities => activities;

        public void Dispose() => listener.Dispose();
    }
}
