using System.Text.Json;
using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Exceptions;
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
public sealed class FailImmediatelyOnPermanentProviderErrorTests(NotificationIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string processingNamespace = $"it-{Guid.CreateVersion7():N}";

    private static string ExchangeBase => "notification.integration.events";

    private string Exchange => RabbitMqResourceNames.Compose(ExchangeBase, processingNamespace);

    [Fact(DisplayName = nameof(PermanentProviderFailureStopsWithoutRetry))]
    public async Task PermanentProviderFailureStopsWithoutRetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        var outputQueue = $"notification.integration.delivery-failed.{Guid.CreateVersion7():N}";
        var emailSender = new PermanentFailureEmailSender();
        using var host = CreateHost(
            emailSender,
            ExchangeBase,
            providerDeliveryLimit: 3,
            pollingIntervalMilliseconds: 5,
            initialBackoffMilliseconds: 25,
            maximumBackoffMilliseconds: 100);

        await host.StartAsync(cancellationToken);
        try
        {
            var connectionProvider = host.Services.GetRequiredService<RabbitMqConnectionProvider>();
            await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
            await DeclareOutputQueueAsync(channel, Exchange, outputQueue, cancellationToken);

            var request = CreateRequest(tenantId, requestId);
            await PublishRequestAsync(channel, Exchange, request, cancellationToken);

            await emailSender.FirstAttempt.Task.WaitAsync(
                TimeSpan.FromSeconds(15),
                cancellationToken);

            var deliveryRecord = await WaitForDeliveryRecordAsync(
                tenantId,
                requestId,
                DeliveryStatus.Failed,
                cancellationToken);
            Assert.Equal(1, deliveryRecord.ProviderAttemptCount);
            Assert.False(deliveryRecord.ExhaustedAttempts);
            Assert.Equal(
                NotificationFailureReasons.PermanentProviderFailure,
                deliveryRecord.Reason);
            Assert.NotNull(deliveryRecord.FailedOn);
            Assert.Null(deliveryRecord.Link);

            var published = await WaitForPublishedMessageAsync(
                channel,
                outputQueue,
                cancellationToken);
            await channel.BasicAckAsync(
                published.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);

            var failed = JsonSerializer.Deserialize<NotificationDeliveryFailedV1>(
                published.Body.Span,
                JsonOptions);
            Assert.NotNull(failed);
            Assert.Equal(requestId, failed!.PedidoId);
            Assert.Equal(tenantId, failed.TenantId);
            Assert.Equal(request.Finalidade, failed.Finalidade);
            Assert.Equal(
                NotificationFailureReasons.PermanentProviderFailure,
                failed.Motivo);
            Assert.False(failed.EsgotouTentativas);

            var outboxMessageId = Guid.Parse(published.BasicProperties.MessageId!);
            var outbox = await WaitForOutboxMessageAsync(
                tenantId,
                outboxMessageId,
                cancellationToken);
            Assert.Equal("NotificationDeliveryFailedV1", outbox.Type);
            Assert.Equal("notificacao.entrega-falhou.v1", outbox.RoutingKey);
            Assert.NotNull(outbox.ProcessedOn);

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            Assert.Equal(1, emailSender.AttemptCount);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private IHost CreateHost(
        PermanentFailureEmailSender emailSender,
        string exchangeBase,
        int providerDeliveryLimit,
        int pollingIntervalMilliseconds,
        int initialBackoffMilliseconds,
        int maximumBackoffMilliseconds)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["Notification:Namespace"] = processingNamespace,
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = exchangeBase,
            ["RabbitMq:DeadLetterExchange"] = $"{exchangeBase}.dlx",
            ["RabbitMq:HeartbeatQueue"] = $"{exchangeBase}.heartbeat",
            ["RabbitMq:SendRequestQueue"] = $"{exchangeBase}.send-request",
            ["RabbitMq:SendRequestRoutingKey"] = "notificacao.envio-solicitado.v1",
            ["RabbitMq:ProviderDeliveryLimit"] = providerDeliveryLimit.ToString(),
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Outbox:BatchSize"] = "10",
            ["Outbox:MaxAttempts"] = "3",
            ["Delivery:PollingIntervalSeconds"] = "1",
            ["Delivery:PollingIntervalMilliseconds"] = pollingIntervalMilliseconds.ToString(),
            ["Delivery:InitialBackoffMilliseconds"] = initialBackoffMilliseconds.ToString(),
            ["Delivery:BackoffMultiplier"] = "2",
            ["Delivery:MaximumBackoffMilliseconds"] = maximumBackoffMilliseconds.ToString(),
            ["Email:SendingDomain"] = "example.invalid",
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
                services.AddSingleton<ITransactionalEmailSender>(emailSender);
            })
            .Build();
    }

    private static NotificationSendRequestedV1 CreateRequest(Guid tenantId, Guid requestId)
        => new(
            requestId,
            tenantId,
            "student@example.com",
            "confirmacao-de-conta",
            "confirmacao-de-conta",
            new NotificationTemplateDataV1(
                "Ana Souza",
                $"https://accounts.example.invalid/confirm?token={requestId:N}"),
            DateTimeOffset.UtcNow);

    private static async Task DeclareOutputQueueAsync(
        IChannel channel,
        string exchange,
        string outputQueue,
        CancellationToken cancellationToken)
    {
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
            "notificacao.entrega-falhou.v1",
            arguments: null,
            cancellationToken: cancellationToken);
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

    private async Task<DeliveryRecord> WaitForDeliveryRecordAsync(
        Guid tenantId,
        Guid requestId,
        DeliveryStatus expectedStatus,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var tenantContext = new TenantContext(processingNamespace);
            tenantContext.Set(tenantId);
            var options = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(fixture.PostgreSql.GetConnectionString())
                .Options;
            await using var dbContext = new NotificationDbContext(options, tenantContext);
            var record = await dbContext.DeliveryRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(record => record.RequestId == requestId, cancellationToken);
            if (record?.Status == expectedStatus)
            {
                return record;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException(
            $"Delivery record for request {requestId} did not reach status {expectedStatus}.");
    }

    private async Task<OutboxMessage> WaitForOutboxMessageAsync(
        Guid tenantId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var tenantContext = new TenantContext(processingNamespace);
            tenantContext.Set(tenantId);
            var options = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(fixture.PostgreSql.GetConnectionString())
                .Options;
            await using var dbContext = new NotificationDbContext(options, tenantContext);
            var message = await dbContext.OutboxMessages
                .AsNoTracking()
                .SingleOrDefaultAsync(outbox => outbox.Id == messageId, cancellationToken);
            if (message?.ProcessedOn is not null)
            {
                return message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException($"Outbox message {messageId} was not processed.");
    }

    private static async Task<BasicGetResult> WaitForPublishedMessageAsync(
        IChannel channel,
        string queue,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: false, cancellationToken);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException("The failed notification was not published.");
    }

    private sealed class PermanentFailureEmailSender : ITransactionalEmailSender
    {
        private int attemptCount;

        public TaskCompletionSource<int> FirstAttempt { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int AttemptCount => Volatile.Read(ref attemptCount);

        public Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref attemptCount);
            FirstAttempt.TrySetResult(attempt);
            return Task.FromException(
                new TransactionalEmailSendException(
                    NotificationFailureReasons.PermanentProviderFailure,
                    isTransient: false));
        }
    }
}
