using System.Collections;
using System.Text;
using System.Text.Json;
using CodeForCoders.Audit.Application;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Infra.Data;
using CodeForCoders.Audit.Infra.Data.Configuration;
using CodeForCoders.Audit.Infra.Data.Queries;
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
public sealed class AuditComplementRecordingTests(AuditIntegrationFixture fixture)
{
    private const string Exchange = "audit.events";
    private const string RoutingKey = "auditoria.registro.complemento-confirmado.v1";
    private const string ActsRoutingKey = "auditoria.ato-praticado.v1";
    private const string ActsQueue = "audit.complement-tests.acts";
    private const string Explanation = "The access change was verified against the internal support case.";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = nameof(RecordsAComplementAfterCommitAndLeavesTheOriginalUnchanged))]
    public async Task RecordsAComplementAfterCommitAndLeavesTheOriginalUnchanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = NewComplementQueue();
        using var host = await StartHostAsync(fixture, cancellationToken, complementQueue: queue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var (act, original) = await CreateOriginalAsync(cancellationToken);
        var before = Serialize(original);
        var complement = CreateComplement(act.TenantId, original.Id);

        await PublishComplementAsync(complement, cancellationToken);
        await WaitForComplementCountAsync(complement.TenantId, complement.ConfirmationId, 1, cancellationToken);

        var recorded = await GetComplementAsync(complement.TenantId, complement.ConfirmationId, cancellationToken);
        var after = await GetOriginalAsync(act.TenantId, original.Id, cancellationToken);
        Assert.Equal(AuditRecord.ComplementRecordType, recorded.RecordType);
        Assert.Equal(original.Id, recorded.OriginalRecordId);
        Assert.Equal(complement.ConfirmationId, recorded.ConfirmationId);
        Assert.Equal(complement.Author.Id, recorded.AuthorId);
        Assert.Equal(TruncateToMicrosecond(complement.ConfirmedAt), recorded.ConfirmedAt);
        Assert.Equal(Explanation, recorded.Explanation);
        Assert.Equal(before, Serialize(after));
        Assert.Equal(0u, await GetQueueMessageCountAsync($"{queue}.dlq", cancellationToken));
    }

    [Fact(DisplayName = nameof(AcknowledgesAnIdenticalRedeliveryWithoutAddingAnotherRow))]
    public async Task AcknowledgesAnIdenticalRedeliveryWithoutAddingAnotherRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = NewComplementQueue();
        using var host = await StartHostAsync(fixture, cancellationToken, complementQueue: queue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var (act, original) = await CreateOriginalAsync(cancellationToken);
        var complement = CreateComplement(act.TenantId, original.Id);
        Assert.NotEqual(0, complement.ConfirmedAt.Ticks % 10);

        await PublishComplementAsync(complement, cancellationToken);
        await WaitForComplementCountAsync(complement.TenantId, complement.ConfirmationId, 1, cancellationToken);
        await PublishComplementAsync(complement, cancellationToken);
        await WaitForQueueToDrainAsync(queue, cancellationToken);

        Assert.Equal(1, await GetComplementCountAsync(complement.TenantId, complement.ConfirmationId, cancellationToken));
        Assert.Equal(0u, await GetQueueMessageCountAsync($"{queue}.dlq", cancellationToken));
    }

    [Fact(DisplayName = nameof(SendsAConfirmationConflictToTheDeadLetterQueueWithoutMutation))]
    public async Task SendsAConfirmationConflictToTheDeadLetterQueueWithoutMutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = NewComplementQueue();
        using var host = await StartHostAsync(fixture, cancellationToken, complementQueue: queue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var (act, original) = await CreateOriginalAsync(cancellationToken);
        var complement = CreateComplement(act.TenantId, original.Id);

        await PublishComplementAsync(complement, cancellationToken);
        await WaitForComplementCountAsync(complement.TenantId, complement.ConfirmationId, 1, cancellationToken);
        await PublishComplementAsync(complement with { Explanation = "A conflicting explanation." }, cancellationToken);
        var deadLetter = await TakeDeadLetterAsync($"{queue}.dlq", cancellationToken);

        Assert.Equal("rejected", deadLetter.Reason);
        Assert.Equal(1, await GetComplementCountAsync(complement.TenantId, complement.ConfirmationId, cancellationToken));
        Assert.Contains("A conflicting explanation.", Encoding.UTF8.GetString(deadLetter.Body), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(SendsAComplementWithNoOriginalToTheDeadLetterQueue))]
    public async Task SendsAComplementWithNoOriginalToTheDeadLetterQueue()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = NewComplementQueue();
        using var host = await StartHostAsync(fixture, cancellationToken, complementQueue: queue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var tenantId = Guid.CreateVersion7();
        var complement = CreateComplement(tenantId, Guid.CreateVersion7());

        await PublishComplementAsync(complement, cancellationToken);
        var deadLetter = await TakeDeadLetterAsync($"{queue}.dlq", cancellationToken);

        Assert.Equal("rejected", deadLetter.Reason);
        Assert.Equal(0, await GetComplementCountAsync(tenantId, complement.ConfirmationId, cancellationToken));
    }

    [Fact(DisplayName = nameof(RejectsAnOriginalThatBelongsToAnotherTenant))]
    public async Task RejectsAnOriginalThatBelongsToAnotherTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = NewComplementQueue();
        using var host = await StartHostAsync(fixture, cancellationToken, complementQueue: queue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var (act, original) = await CreateOriginalAsync(cancellationToken);
        var otherTenantComplement = CreateComplement(Guid.CreateVersion7(), original.Id);

        await PublishComplementAsync(otherTenantComplement, cancellationToken);
        var deadLetter = await TakeDeadLetterAsync($"{queue}.dlq", cancellationToken);

        Assert.Equal("rejected", deadLetter.Reason);
        Assert.Equal(0, await GetComplementCountAsync(otherTenantComplement.TenantId, otherTenantComplement.ConfirmationId, cancellationToken));
        Assert.Equal(0u, await GetQueueMessageCountAsync(ActsQueue + ".dlq", cancellationToken));
    }

    [Fact(DisplayName = nameof(RejectsAnotherComplementUsedAsTheOriginal))]
    public async Task RejectsAnotherComplementUsedAsTheOriginal()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = NewComplementQueue();
        using var host = await StartHostAsync(fixture, cancellationToken, complementQueue: queue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var (act, original) = await CreateOriginalAsync(cancellationToken);
        var first = CreateComplement(act.TenantId, original.Id);
        await PublishComplementAsync(first, cancellationToken);
        await WaitForComplementCountAsync(first.TenantId, first.ConfirmationId, 1, cancellationToken);
        var firstRecord = await GetComplementAsync(first.TenantId, first.ConfirmationId, cancellationToken);
        var second = CreateComplement(act.TenantId, firstRecord.Id);

        await PublishComplementAsync(second, cancellationToken);
        var deadLetter = await TakeDeadLetterAsync($"{queue}.dlq", cancellationToken);

        Assert.Equal("rejected", deadLetter.Reason);
        Assert.Equal(0, await GetComplementCountAsync(second.TenantId, second.ConfirmationId, cancellationToken));
        Assert.Equal(1, await GetComplementCountAsync(first.TenantId, first.ConfirmationId, cancellationToken));
    }

    [Fact(DisplayName = nameof(DisplaysComplementsInConfirmedAtOrderWhenMessagesArriveOutOfOrder))]
    public async Task DisplaysComplementsInConfirmedAtOrderWhenMessagesArriveOutOfOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = NewComplementQueue();
        using var host = await StartHostAsync(fixture, cancellationToken, complementQueue: queue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var (act, original) = await CreateOriginalAsync(cancellationToken);
        var now = TimeProvider.System.GetUtcNow();
        var early = CreateComplement(act.TenantId, original.Id, confirmedAt: now.AddMinutes(-2), explanation: "First apuration.");
        var late = CreateComplement(act.TenantId, original.Id, confirmedAt: now.AddMinutes(-1), explanation: "Second apuration.");

        await PublishComplementAsync(late, cancellationToken);
        await WaitForComplementCountAsync(late.TenantId, late.ConfirmationId, 1, cancellationToken);
        await PublishComplementAsync(early, cancellationToken);
        await WaitForComplementCountAsync(early.TenantId, early.ConfirmationId, 1, cancellationToken);

        await using var dbContext = CreateDbContext(fixture);
        var detail = await new AuditRecordDetailQueries(dbContext)
            .FindOriginalWithComplementsAsync(act.TenantId, original.Id, cancellationToken);
        Assert.NotNull(detail);
        Assert.Equal(
            new Guid?[] { early.ConfirmationId, late.ConfirmationId },
            detail.Complements.Select(record => record.ConfirmationId).ToArray());
    }

    [Fact(DisplayName = nameof(BasicRejectCountsTransientFailuresTowardRabbitMq43DeliveryLimit))]
    public async Task BasicRejectCountsTransientFailuresTowardRabbitMq43DeliveryLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = $"audit.complement-tests.retry.{Guid.CreateVersion7():N}";
        var actsQueue = $"audit.complement-tests.retry.acts.{Guid.CreateVersion7():N}";
        var connectionString = "Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Timeout=1";
        using var host = await StartHostAsync(fixture, cancellationToken, connectionString, queue, deliveryLimit: 2, actsQueue: actsQueue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var complement = CreateComplement(Guid.CreateVersion7(), Guid.CreateVersion7());

        await PublishComplementAsync(complement, cancellationToken);
        var deadLetter = await TakeDeadLetterAsync($"{queue}.dlq", cancellationToken);

        Assert.Equal("delivery_limit", deadLetter.Reason);
        Assert.Contains(complement.ConfirmationId.ToString(), Encoding.UTF8.GetString(deadLetter.Body), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(ScopesConfirmationUniquenessToTheTenant))]
    public async Task ScopesConfirmationUniquenessToTheTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var queue = NewComplementQueue();
        using var host = await StartHostAsync(fixture, cancellationToken, complementQueue: queue);
        await WaitForConsumersAsync(queue, cancellationToken);
        var (firstAct, firstOriginal) = await CreateOriginalAsync(cancellationToken);
        var (secondAct, secondOriginal) = await CreateOriginalAsync(cancellationToken);
        var sharedConfirmationId = Guid.CreateVersion7();
        var first = CreateComplement(firstAct.TenantId, firstOriginal.Id, sharedConfirmationId);
        var second = CreateComplement(secondAct.TenantId, secondOriginal.Id, sharedConfirmationId, explanation: "A different tenant explanation.");

        await PublishComplementAsync(first, cancellationToken);
        await WaitForComplementCountAsync(first.TenantId, sharedConfirmationId, 1, cancellationToken);
        await PublishComplementAsync(second, cancellationToken);
        await WaitForComplementCountAsync(second.TenantId, sharedConfirmationId, 1, cancellationToken);

        Assert.Equal(1, await GetComplementCountAsync(first.TenantId, sharedConfirmationId, cancellationToken));
        Assert.Equal(1, await GetComplementCountAsync(second.TenantId, sharedConfirmationId, cancellationToken));
        Assert.Equal(0u, await GetQueueMessageCountAsync($"{queue}.dlq", cancellationToken));
    }

    [Fact(DisplayName = nameof(RuntimeCredentialCanOnlyReadAndInsertAuditRows))]
    public async Task RuntimeCredentialCanOnlyReadAndInsertAuditRows()
    {
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, @table, 'SELECT'), has_table_privilege(current_user, @table, 'INSERT'), has_table_privilege(current_user, @table, 'UPDATE'), has_table_privilege(current_user, @table, 'DELETE')",
            connection);
        command.Parameters.AddWithValue("table", $"{AuditSchema.Name}.audit_records");
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }

    private static async Task<IHost> StartHostAsync(
        AuditIntegrationFixture fixture,
        CancellationToken cancellationToken,
        string? connectionString = null,
        string complementQueue = "audit.complement-tests.complements",
        int deliveryLimit = 5,
        string actsQueue = ActsQueue)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString ?? fixture.RuntimeConnectionString,
            ["AuditDatabase:WriterRole"] = AuditIntegrationFixture.WriterRole,
            ["AuditSnapshots:ConnectionString"] = "localhost:6379,abortConnect=false",
            ["AuditSnapshots:KeyPrefix"] = "audit:complement-tests:",
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = Exchange,
            ["RabbitMq:DeadLetterExchange"] = "audit.events.dlx",
            ["RabbitMq:AuditQueue"] = actsQueue,
            ["RabbitMq:EventRoutingKey"] = ActsRoutingKey,
            ["RabbitMq:AuditComplementQueue"] = complementQueue,
            ["RabbitMq:AuditComplementEventRoutingKey"] = RoutingKey,
            ["RabbitMq:DeliveryLimit"] = deliveryLimit.ToString(),
        };

        var host = Host.CreateDefaultBuilder()
            .UseEnvironment("IntegrationTest")
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(configurationValues))
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationConfiguration();
                services.AddDataConfiguration(context.Configuration, context.HostingEnvironment);
                services.AddMessagingConfiguration(context.Configuration);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(TimeProvider.System);
            })
            .Build();

        await host.StartAsync(cancellationToken);
        return host;
    }

    private async Task<(AtoPraticado Act, AuditRecord Original)> CreateOriginalAsync(CancellationToken cancellationToken)
    {
        var act = CreateAct(Guid.CreateVersion7());
        await PublishActAsync(act, cancellationToken);
        for (var attempt = 0; attempt < 150; attempt++)
        {
            var record = await FindOriginalByFactAsync(act, cancellationToken);
            if (record is not null)
            {
                return (act, record);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("The source audit act was not stored.");
    }

    private async Task PublishActAsync(AtoPraticado act, CancellationToken cancellationToken)
    {
        var factory = CreateConnectionFactory();
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = act.FatoId.ToString(),
            Type = "AtoPraticado",
        };
        await channel.BasicPublishAsync(
            Exchange,
            ActsRoutingKey,
            mandatory: true,
            properties,
            JsonSerializer.SerializeToUtf8Bytes(act, SerializerOptions),
            cancellationToken);
    }

    private async Task PublishComplementAsync(
        ComplementoConfirmadoV1 complement,
        CancellationToken cancellationToken)
    {
        var factory = CreateConnectionFactory();
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = complement.ConfirmationId.ToString(),
            Type = "ComplementoConfirmado",
        };
        await channel.BasicPublishAsync(
            Exchange,
            RoutingKey,
            mandatory: true,
            properties,
            JsonSerializer.SerializeToUtf8Bytes(complement, SerializerOptions),
            cancellationToken);
    }

    private async Task WaitForConsumersAsync(string queue, CancellationToken cancellationToken)
    {
        await using var connection = await CreateConnectionFactory().CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var info = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
            if (info.ConsumerCount > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The audit complement consumer did not start.");
    }

    private async Task WaitForComplementCountAsync(
        Guid tenantId,
        Guid confirmationId,
        long expectedCount,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (await GetComplementCountAsync(tenantId, confirmationId, cancellationToken) == expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("The expected audit complement count was not reached.");
    }

    private async Task WaitForQueueToDrainAsync(string queue, CancellationToken cancellationToken)
    {
        await using var connection = await CreateConnectionFactory().CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var info = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
            if (info.MessageCount == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The audit complement queue did not drain.");
    }

    private async Task<DeadLetterMessage> TakeDeadLetterAsync(string queue, CancellationToken cancellationToken)
    {
        await using var connection = await CreateConnectionFactory().CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        for (var attempt = 0; attempt < 400; attempt++)
        {
            var delivery = await channel.BasicGetAsync(queue, autoAck: false, cancellationToken);
            if (delivery is not null)
            {
                var message = new DeadLetterMessage(delivery.Body.ToArray(), GetDeadLetterReason(delivery.BasicProperties));
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                return message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The audit complement did not reach its dead-letter queue.");
    }

    private async Task<uint> GetQueueMessageCountAsync(string queue, CancellationToken cancellationToken)
    {
        await using var connection = await CreateConnectionFactory().CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var info = await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
        return info.MessageCount;
    }

    private async Task<long> GetComplementCountAsync(Guid tenantId, Guid confirmationId, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(fixture);
        return await dbContext.AuditRecords.AsNoTracking()
            .LongCountAsync(record => record.TenantId == tenantId && record.ConfirmationId == confirmationId, cancellationToken);
    }

    private async Task<AuditRecord> GetComplementAsync(Guid tenantId, Guid confirmationId, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(fixture);
        return await dbContext.AuditRecords.AsNoTracking()
            .SingleAsync(record => record.TenantId == tenantId && record.ConfirmationId == confirmationId, cancellationToken);
    }

    private async Task<AuditRecord?> FindOriginalByFactAsync(AtoPraticado act, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(fixture);
        return await dbContext.AuditRecords.AsNoTracking()
            .SingleOrDefaultAsync(record => record.Origin == act.Origem && record.FactId == act.FatoId, cancellationToken);
    }

    private async Task<AuditRecord> GetOriginalAsync(Guid tenantId, Guid recordId, CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext(fixture);
        return await dbContext.AuditRecords.AsNoTracking()
            .SingleAsync(record => record.TenantId == tenantId && record.Id == recordId, cancellationToken);
    }

    private static string Serialize(AuditRecord record) => JsonSerializer.Serialize(record);

    private static string NewComplementQueue() => $"audit.complement-tests.complements.{Guid.CreateVersion7():N}";

    private static AuditDbContext CreateDbContext(AuditIntegrationFixture fixture)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.RuntimeConnectionString)
            .Options;
        return new AuditDbContext(options);
    }

    private ConnectionFactory CreateConnectionFactory()
        => new()
        {
            HostName = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            UserName = "code_for_coders",
            Password = "code_for_coders",
        };

    private static AtoPraticado CreateAct(Guid tenantId)
        => new()
        {
            FatoId = Guid.CreateVersion7(),
            Origem = "identidade",
            Tipo = "papel-concedido",
            TenantId = tenantId,
            PraticadoEm = TimeProvider.System.GetUtcNow().AddMinutes(-5),
            Autor = new ReferenciaAto { Tipo = "conta-interna", Id = Guid.CreateVersion7() },
            Alvo = new ReferenciaAto { Tipo = "conta-interna", Id = Guid.CreateVersion7() },
            Complemento = new Dictionary<string, string> { ["papel"] = "professor" },
            Motivo = "A access change was verified.",
        };

    private static ComplementoConfirmadoV1 CreateComplement(
        Guid tenantId,
        Guid originalRecordId,
        Guid? confirmationId = null,
        DateTimeOffset? confirmedAt = null,
        string explanation = Explanation)
    {
        // Keeps the 100 ns resolution the BFF produces, below what PostgreSQL stores.
        var timestamp = TruncateToMicrosecond(confirmedAt ?? TimeProvider.System.GetUtcNow()).AddTicks(7);
        return new ComplementoConfirmadoV1(
            confirmationId ?? Guid.CreateVersion7(timestamp),
            tenantId,
            originalRecordId,
            timestamp,
            new ComplementAuthorV1("conta-interna", Guid.CreateVersion7()),
            explanation);
    }

    private static DateTimeOffset TruncateToMicrosecond(DateTimeOffset value)
        => new(value.Ticks - value.Ticks % 10, TimeSpan.Zero);

    private static string? GetDeadLetterReason(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers?.TryGetValue("x-death", out var value) != true || value is not IEnumerable entries)
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (entry is not IDictionary death || !death.Contains("reason"))
            {
                continue;
            }

            var reason = death["reason"];
            return reason is byte[] bytes ? Encoding.UTF8.GetString(bytes) : reason?.ToString();
        }

        return null;
    }

    private sealed record DeadLetterMessage(byte[] Body, string? Reason);
}
