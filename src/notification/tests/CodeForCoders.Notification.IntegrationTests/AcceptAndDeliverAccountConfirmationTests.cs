using System.Text;
using System.Text.Json;
using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Application.Common;
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
public sealed class AcceptAndDeliverAccountConfirmationTests(NotificationIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string processingNamespace = $"it-{Guid.CreateVersion7():N}";

    private string Exchange
        => RabbitMqResourceNames.Compose("notification.integration.events", processingNamespace);

    [Fact(DisplayName = nameof(AccountConfirmationRequestIsAcceptedDeliveredAndPublished))]
    public async Task AccountConfirmationRequestIsAcceptedDeliveredAndPublished()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        var correlationId = "identity-account-confirmation-0001";
        var request = new NotificationSendRequestedV1(
            requestId,
            tenantId,
            "student@example.com",
            "confirmacao-de-conta",
            "confirmacao-de-conta",
            new NotificationTemplateDataV1(
                "Ana Souza",
                "https://accounts.example.invalid/confirm?token=abc123"),
            DateTimeOffset.UtcNow);
        var emailSender = new FakeTransactionalEmailSender();
        var outputQueue = $"notification.integration.delivered.{Guid.CreateVersion7():N}";
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["Notification:Namespace"] = processingNamespace,
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = "notification.integration.events",
            ["RabbitMq:DeadLetterExchange"] = "notification.integration.events.dlx",
            ["RabbitMq:HeartbeatQueue"] = "notification.integration.platform-heartbeat",
            ["RabbitMq:SendRequestQueue"] = "notification.integration.send-request",
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

        using var host = Host.CreateDefaultBuilder()
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

        await host.StartAsync(cancellationToken);
        try
        {
            var connectionProvider = host.Services.GetRequiredService<RabbitMqConnectionProvider>();
            await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
            await channel.QueueDeclareAsync(
                outputQueue,
                durable: false,
                exclusive: true,
                autoDelete: true,
                arguments: null,
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(
                outputQueue,
                Exchange,
                "notificacao.mensagem-entregue.v1",
                arguments: null,
                cancellationToken: cancellationToken);

            await PublishRequestAsync(channel, Exchange, request, correlationId, cancellationToken);

            var email = await emailSender.SentEmail.Task.WaitAsync(
                TimeSpan.FromSeconds(15),
                cancellationToken);
            Assert.Equal(request.Destinatario, email.To);
            Assert.Equal("Confirm your account", email.Subject);
            Assert.Contains(request.Dados!.Nome!, email.TextBody, StringComparison.Ordinal);
            Assert.Contains(request.Dados.Link!, email.TextBody, StringComparison.Ordinal);
            Assert.Contains("24 hours", email.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain("unsubscribe", email.TextBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("descadastro", email.TextBody, StringComparison.OrdinalIgnoreCase);

            var deliveryRecord = await WaitForDeliveryRecordAsync(
                tenantId,
                requestId,
                cancellationToken);
            Assert.Equal(DeliveryStatus.Delivered, deliveryRecord.Status);
            Assert.True(deliveryRecord.AcceptedOn!.Value >= deliveryRecord.RequestedOn);
            Assert.NotNull(deliveryRecord.DeliveredOn);
            Assert.True(deliveryRecord.DeliveredOn!.Value >= deliveryRecord.AcceptedOn!.Value);
            Assert.Null(deliveryRecord.Link);

            var published = await WaitForPublishedMessageAsync(
                channel,
                outputQueue,
                cancellationToken);
            await channel.BasicAckAsync(
                published.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);

            var rawPayload = Encoding.UTF8.GetString(published.Body.Span);
            var delivered = JsonSerializer.Deserialize<NotificationMessageDeliveredV1>(
                published.Body.Span,
                JsonOptions);
            Assert.NotNull(delivered);
            Assert.Equal(requestId, delivered!.PedidoId);
            Assert.Equal(tenantId, delivered.TenantId);
            Assert.Equal(request.Finalidade, delivered.Finalidade);
            Assert.Equal(request.Destinatario, delivered.Destinatario);
            Assert.Equal("email", delivered.Canal);
            Assert.DoesNotContain(request.Dados.Link!, rawPayload, StringComparison.Ordinal);
            Assert.DoesNotContain(request.Dados.Nome!, rawPayload, StringComparison.Ordinal);
            Assert.DoesNotContain("textBody", rawPayload, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(correlationId, published.BasicProperties.CorrelationId);

            var outboxMessageId = Guid.Parse(published.BasicProperties.MessageId!);
            var outbox = await WaitForOutboxMessageAsync(
                tenantId,
                outboxMessageId,
                cancellationToken);
            Assert.Equal("NotificationMessageDeliveredV1", outbox.Type);
            Assert.Equal("notificacao.mensagem-entregue.v1", outbox.RoutingKey);
            Assert.NotNull(outbox.ProcessedOn);
            Assert.DoesNotContain(request.Dados.Link!, outbox.Payload, StringComparison.Ordinal);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static async Task PublishRequestAsync(
        IChannel channel,
        string exchange,
        NotificationSendRequestedV1 request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = correlationId,
        };
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: "notificacao.envio-solicitado.v1",
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions),
            cancellationToken: cancellationToken);
    }

    private async Task<DeliveryRecord> WaitForDeliveryRecordAsync(
        Guid tenantId,
        Guid requestId,
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
                .SingleOrDefaultAsync(record => record.RequestId == requestId, cancellationToken);
            if (record?.Status == DeliveryStatus.Delivered)
            {
                return record;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException($"Delivery record for request {requestId} was not delivered.");
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
                .SingleOrDefaultAsync(outbox => outbox.Id == messageId, cancellationToken);
            if (message?.ProcessedOn is not null)
            {
                return message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
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

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException("The delivered notification was not published.");
    }

    private sealed class FakeTransactionalEmailSender : ITransactionalEmailSender
    {
        public TaskCompletionSource<TransactionalEmail> SentEmail { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken)
        {
            SentEmail.TrySetResult(email);
            return Task.CompletedTask;
        }
    }
}
