using System.Text.Json;
using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Contracts;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Infra.Data;
using CodeForCoders.Notification.Infra.Data.Outbox;
using CodeForCoders.Notification.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Notification.IntegrationTests;

[Collection(NotificationIntegrationCollection.Name)]
public sealed class ConcurrentNamespaceIsolationTests(NotificationIntegrationFixture fixture)
{
    private const string EventsExchangeBase = "notification.integration.events";
    private const string DeadLetterExchangeBase = "notification.integration.events.dlx";
    private const string HeartbeatQueueBase = "notification.integration.platform-heartbeat";
    private const string SendRequestQueueBase = "notification.integration.send-request";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = nameof(ConcurrentHostsOnlyProcessTheirOwnNamespace))]
    public async Task ConcurrentHostsOnlyProcessTheirOwnNamespace()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstNamespace = $"it-iso-a-{Guid.CreateVersion7():N}";
        var secondNamespace = $"it-iso-b-{Guid.CreateVersion7():N}";
        var firstSender = new RecordingEmailSender();
        var secondSender = new RecordingEmailSender();
        using var firstHost = CreateHost(firstNamespace, firstSender);
        using var secondHost = CreateHost(secondNamespace, secondSender);
        var firstRequest = CreateRequest("host-a@example.com");
        var secondRequest = CreateRequest("host-b@example.com");

        await firstHost.StartAsync(cancellationToken);
        await secondHost.StartAsync(cancellationToken);
        try
        {
            var connectionProvider = secondHost.Services.GetRequiredService<RabbitMqConnectionProvider>();
            await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
            var firstExchange = RabbitMqResourceNames.Compose(EventsExchangeBase, firstNamespace);
            var secondExchange = RabbitMqResourceNames.Compose(EventsExchangeBase, secondNamespace);
            var firstOutputQueue = await DeclareOutputQueueAsync(channel, firstExchange, cancellationToken);
            var secondOutputQueue = await DeclareOutputQueueAsync(channel, secondExchange, cancellationToken);

            await PublishRequestAsync(channel, firstExchange, firstRequest, cancellationToken);
            await PublishRequestAsync(channel, secondExchange, secondRequest, cancellationToken);

            await WaitForDeliveredAsync(firstNamespace, firstRequest.PedidoId, cancellationToken);
            await WaitForDeliveredAsync(secondNamespace, secondRequest.PedidoId, cancellationToken);

            Assert.Single(firstSender.SentEmails, email => email.To == firstRequest.Destinatario);
            Assert.Single(secondSender.SentEmails, email => email.To == secondRequest.Destinatario);
            await AssertOutboxOnlyContainsNamespaceAsync(firstNamespace, [firstRequest.PedidoId], cancellationToken);
            await AssertOutboxOnlyContainsNamespaceAsync(secondNamespace, [secondRequest.PedidoId], cancellationToken);

            var firstEvent = await ReadSinglePublishedEventAsync(channel, firstOutputQueue, cancellationToken);
            Assert.Equal(firstRequest.PedidoId, ExtractPedidoId(firstEvent));
            var secondEvent = await ReadSinglePublishedEventAsync(channel, secondOutputQueue, cancellationToken);
            Assert.Equal(secondRequest.PedidoId, ExtractPedidoId(secondEvent));
            await channel.BasicAckAsync(firstEvent.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
            await channel.BasicAckAsync(secondEvent.DeliveryTag, multiple: false, cancellationToken: cancellationToken);

            await CleanupNamespaceAsync(channel, firstNamespace, cancellationToken);
            await AssertNamespaceIsEmptyAsync(firstNamespace, cancellationToken);
            await AssertOutboxOnlyContainsNamespaceAsync(secondNamespace, [secondRequest.PedidoId], cancellationToken);
        }
        finally
        {
            await secondHost.StopAsync(CancellationToken.None);
            await firstHost.StopAsync(CancellationToken.None);
        }
    }

    [Fact(DisplayName = nameof(SurvivingNamespaceKeepsProcessingAfterAnotherNamespaceIsPurged))]
    public async Task SurvivingNamespaceKeepsProcessingAfterAnotherNamespaceIsPurged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var purgedNamespace = $"it-iso-purge-{Guid.CreateVersion7():N}";
        var survivorNamespace = $"it-iso-keep-{Guid.CreateVersion7():N}";
        var purgedSender = new RecordingEmailSender();
        var survivorSender = new RecordingEmailSender();
        using var purgedHost = CreateHost(purgedNamespace, purgedSender);
        using var survivorHost = CreateHost(survivorNamespace, survivorSender);
        var purgedRequest = CreateRequest("purged@example.com");
        var survivorRequest = CreateRequest("survivor@example.com");

        await purgedHost.StartAsync(cancellationToken);
        await survivorHost.StartAsync(cancellationToken);
        try
        {
            var connectionProvider = survivorHost.Services.GetRequiredService<RabbitMqConnectionProvider>();
            await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
            var purgedExchange = RabbitMqResourceNames.Compose(EventsExchangeBase, purgedNamespace);
            var survivorExchange = RabbitMqResourceNames.Compose(EventsExchangeBase, survivorNamespace);
            var survivorOutputQueue = await DeclareOutputQueueAsync(channel, survivorExchange, cancellationToken);

            await PublishRequestAsync(channel, purgedExchange, purgedRequest, cancellationToken);
            await PublishRequestAsync(channel, survivorExchange, survivorRequest, cancellationToken);
            await WaitForDeliveredAsync(purgedNamespace, purgedRequest.PedidoId, cancellationToken);
            await WaitForDeliveredAsync(survivorNamespace, survivorRequest.PedidoId, cancellationToken);

            await CleanupNamespaceAsync(channel, purgedNamespace, cancellationToken);
            await AssertNamespaceIsEmptyAsync(purgedNamespace, cancellationToken);

            var secondSurvivorRequest = CreateRequest("survivor@example.com");
            await PublishRequestAsync(channel, survivorExchange, secondSurvivorRequest, cancellationToken);
            await WaitForDeliveredAsync(survivorNamespace, secondSurvivorRequest.PedidoId, cancellationToken);
            Assert.Equal(2, survivorSender.SentEmails.Count);
        }
        finally
        {
            await survivorHost.StopAsync(CancellationToken.None);
            await purgedHost.StopAsync(CancellationToken.None);
        }
    }

    private IHost CreateHost(
        string processingNamespace,
        ITransactionalEmailSender emailSender)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["Notification:Namespace"] = processingNamespace,
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = EventsExchangeBase,
            ["RabbitMq:DeadLetterExchange"] = DeadLetterExchangeBase,
            ["RabbitMq:HeartbeatQueue"] = HeartbeatQueueBase,
            ["RabbitMq:SendRequestQueue"] = SendRequestQueueBase,
            ["RabbitMq:SendRequestRoutingKey"] = "notificacao.envio-solicitado.v1",
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Outbox:BatchSize"] = "10",
            ["Outbox:MaxAttempts"] = "3",
            ["Delivery:PollingIntervalSeconds"] = "1",
            ["Email:SendingDomain"] = "example.invalid",
            ["Email:ValidityHoursByPurpose:confirmacao-de-conta"] = "24",
            ["Email:ValidityHoursByPurpose:recuperacao-de-senha"] = "1",
            ["Valkey:ConnectionString"] = "localhost:6379,abortConnect=false",
        };

        return Host.CreateDefaultBuilder()
            .UseEnvironment("IntegrationTest")
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(configurationValues))
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationConfiguration();
                services.AddDataConfiguration(context.Configuration, context.HostingEnvironment);
                services.AddMessagingConfiguration(context.Configuration);
                services.AddNotificationMessageHandlers();
                services.AddSingleton(emailSender);
            })
            .Build();
    }

    private static NotificationSendRequestedV1 CreateRequest(string recipient)
        => new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            recipient,
            "confirmacao-de-conta",
            "confirmacao-de-conta",
            new NotificationTemplateDataV1(
                "Ana Souza",
                $"https://accounts.example.invalid/confirm?token={Guid.CreateVersion7():N}"),
            DateTimeOffset.UtcNow);

    private static async Task<string> DeclareOutputQueueAsync(
        IChannel channel,
        string exchange,
        CancellationToken cancellationToken)
    {
        var outputQueue = $"notification.integration.isolation.{Guid.CreateVersion7():N}";
        await channel.QueueDeclareAsync(
            outputQueue,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            outputQueue,
            exchange,
            "notificacao.mensagem-entregue.v1",
            arguments: null,
            cancellationToken: cancellationToken);
        return outputQueue;
    }

    private static async Task PublishRequestAsync(
        IChannel channel,
        string exchange,
        NotificationSendRequestedV1 request,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
        };
        await channel.BasicPublishAsync(
            exchange,
            "notificacao.envio-solicitado.v1",
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions),
            cancellationToken: cancellationToken);
    }

    private async Task CleanupNamespaceAsync(
        IChannel channel,
        string processingNamespace,
        CancellationToken cancellationToken)
    {
        await fixture.PurgeNamespaceAsync(processingNamespace);
        var sendRequestQueue = RabbitMqResourceNames.Compose(SendRequestQueueBase, processingNamespace);
        var heartbeatQueue = RabbitMqResourceNames.Compose(HeartbeatQueueBase, processingNamespace);
        await channel.QueueDeleteAsync(sendRequestQueue, ifUnused: false, ifEmpty: false, noWait: false, cancellationToken);
        await channel.QueueDeleteAsync($"{sendRequestQueue}.dlq", ifUnused: false, ifEmpty: false, noWait: false, cancellationToken);
        await channel.QueueDeleteAsync(heartbeatQueue, ifUnused: false, ifEmpty: false, noWait: false, cancellationToken);
        await channel.QueueDeleteAsync($"{heartbeatQueue}.dlq", ifUnused: false, ifEmpty: false, noWait: false, cancellationToken);
        await channel.ExchangeDeleteAsync(
            RabbitMqResourceNames.Compose(EventsExchangeBase, processingNamespace),
            ifUnused: false,
            noWait: false,
            cancellationToken);
        await channel.ExchangeDeleteAsync(
            RabbitMqResourceNames.Compose(DeadLetterExchangeBase, processingNamespace),
            ifUnused: false,
            noWait: false,
            cancellationToken);
    }

    private async Task WaitForDeliveredAsync(
        string processingNamespace,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var records = await ReadDeliveryRecordsAsync(processingNamespace, cancellationToken);
            if (records.Any(record => record.RequestId == requestId && record.Status == DeliveryStatus.Delivered))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        var finalRecords = await ReadDeliveryRecordsAsync(processingNamespace, cancellationToken);
        var dump = string.Join(
            ", ",
            finalRecords.Select(record => $"{record.RequestId}:{record.Status}"));
        throw new Xunit.Sdk.XunitException(
            $"Delivery record for request {requestId} was not delivered in namespace {processingNamespace}. Records: [{dump}].");
    }

    private async Task AssertNamespaceIsEmptyAsync(
        string processingNamespace,
        CancellationToken cancellationToken)
    {
        var records = await ReadDeliveryRecordsAsync(processingNamespace, cancellationToken);
        Assert.Empty(records);
        await using var dbContext = await CreateDbContextAsync(processingNamespace);
        var outboxMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        Assert.Empty(outboxMessages);
    }

    private async Task AssertOutboxOnlyContainsNamespaceAsync(
        string processingNamespace,
        IReadOnlyCollection<Guid> expectedRequestIds,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateDbContextAsync(processingNamespace);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var processed = await dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message => message.ProcessedOn != null)
                .ToListAsync(cancellationToken);
            if (processed.Count == expectedRequestIds.Count
                && processed.All(message => message.Namespace == processingNamespace))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException(
            $"Outbox of namespace {processingNamespace} did not settle to {expectedRequestIds.Count} processed messages.");
    }

    private async Task<List<DeliveryRecord>> ReadDeliveryRecordsAsync(
        string processingNamespace,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await CreateDbContextAsync(processingNamespace);
        return await dbContext.DeliveryRecords
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private async Task<NotificationDbContext> CreateDbContextAsync(string processingNamespace)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        return new NotificationDbContext(options, new TenantContext(processingNamespace));
    }

    private static async Task<BasicGetResult> ReadSinglePublishedEventAsync(
        IChannel channel,
        string outputQueue,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(outputQueue, autoAck: false, cancellationToken);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException($"No delivered event was published to queue {outputQueue}.");
    }

    private static Guid ExtractPedidoId(BasicGetResult published)
    {
        var delivered = JsonSerializer.Deserialize<NotificationMessageDeliveredV1>(
            published.Body.Span,
            JsonOptions);
        Assert.NotNull(delivered);
        return delivered!.PedidoId;
    }

    private sealed class RecordingEmailSender : ITransactionalEmailSender
    {
        public List<TransactionalEmail> SentEmails { get; } = [];

        public Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken)
        {
            lock (SentEmails)
            {
                SentEmails.Add(email);
            }

            return Task.CompletedTask;
        }
    }
}
