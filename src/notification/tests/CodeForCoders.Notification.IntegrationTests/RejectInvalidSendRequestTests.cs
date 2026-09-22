using System.Collections.Concurrent;
using System.Text.Json;
using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Contracts;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Infra.Data;
using CodeForCoders.Notification.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Notification.IntegrationTests;

[Collection(NotificationIntegrationCollection.Name)]
public sealed class RejectInvalidSendRequestTests(NotificationIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = nameof(RequestWithoutPurposeIsRefusedWithSpecificReason))]
    public async Task RequestWithoutPurposeIsRefusedWithSpecificReason()
    {
        var request = CreateRequest(finalidade: null);

        await AssertRequestIsRefusedAsync(request, NotificationRefusalReasons.MissingPurpose);
    }

    [Fact(DisplayName = nameof(RequestWithUnknownModelIsRefusedWithSpecificReason))]
    public async Task RequestWithUnknownModelIsRefusedWithSpecificReason()
    {
        var request = CreateRequest(modelo: "modelo-inexistente");

        await AssertRequestIsRefusedAsync(request, NotificationRefusalReasons.UnknownModel);
    }

    [Fact(DisplayName = nameof(RequestWithMissingDataIsRefusedWithSpecificReason))]
    public async Task RequestWithMissingDataIsRefusedWithSpecificReason()
    {
        var request = CreateRequest(
            dados: new NotificationTemplateDataV1(
                Nome: null,
                Link: "https://accounts.example.invalid/confirm?token=missing-name"));

        await AssertRequestIsRefusedAsync(request, NotificationRefusalReasons.MissingData);
    }

    [Fact(DisplayName = nameof(RequestWithInvalidLinkIsRefusedWithSpecificReason))]
    public async Task RequestWithInvalidLinkIsRefusedWithSpecificReason()
    {
        var request = CreateRequest(
            dados: new NotificationTemplateDataV1(
                Nome: "Ana Souza",
                Link: "accounts.example.invalid/confirm?token=relative"));

        await AssertRequestIsRefusedAsync(request, NotificationRefusalReasons.MissingData);
    }

    private async Task AssertRequestIsRefusedAsync(
        NotificationSendRequestedV1 request,
        string expectedReason)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var emailSender = new FakeTransactionalEmailSender();
        var queue = $"notification.integration.send-request.{Guid.CreateVersion7():N}";
        using var host = CreateHost(emailSender, queue);

        await host.StartAsync(cancellationToken);
        try
        {
            var connectionProvider = host.Services.GetRequiredService<RabbitMqConnectionProvider>();
            await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
            await PublishRequestAsync(channel, request, cancellationToken);

            var deliveryRecord = await WaitForRefusedRecordAsync(
                request.TenantId,
                request.PedidoId,
                cancellationToken);
            Assert.Equal(DeliveryStatus.Refused, deliveryRecord.Status);
            Assert.Equal(expectedReason, deliveryRecord.Reason);
            Assert.Null(deliveryRecord.AcceptedOn);
            Assert.NotNull(deliveryRecord.RefusedOn);
            Assert.Null(deliveryRecord.Link);

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            Assert.Empty(emailSender.SentEmails);

            var tenantContext = new TenantContext();
            tenantContext.Set(request.TenantId);
            var dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(fixture.PostgreSql.GetConnectionString())
                .Options;
            await using var dbContext = new NotificationDbContext(dbOptions, tenantContext);
            var deliveredEvents = await dbContext.OutboxMessages
                .Where(message => message.RoutingKey == "notificacao.mensagem-entregue.v1")
                .ToListAsync(cancellationToken);
            Assert.Empty(deliveredEvents);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private IHost CreateHost(
        FakeTransactionalEmailSender emailSender,
        string queue)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = "notification.integration.events",
            ["RabbitMq:DeadLetterExchange"] = "notification.integration.events.dlx",
            ["RabbitMq:HeartbeatQueue"] = $"notification.integration.platform-heartbeat.{Guid.CreateVersion7():N}",
            ["RabbitMq:SendRequestQueue"] = queue,
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
                services.AddSingleton<ITransactionalEmailSender>(emailSender);
            })
            .Build();
    }

    private static NotificationSendRequestedV1 CreateRequest(
        string? finalidade = "confirmacao-de-conta",
        string? modelo = "confirmacao-de-conta",
        NotificationTemplateDataV1? dados = null)
        => new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "student@example.com",
            finalidade,
            modelo,
            dados ?? new NotificationTemplateDataV1(
                "Ana Souza",
                "https://accounts.example.invalid/confirm?token=abc123"),
            DateTimeOffset.UtcNow);

    private static async Task PublishRequestAsync(
        IChannel channel,
        NotificationSendRequestedV1 request,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = $"integration-reject-{request.PedidoId:N}",
        };
        await channel.BasicPublishAsync(
            exchange: "notification.integration.events",
            routingKey: "notificacao.envio-solicitado.v1",
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions),
            cancellationToken: cancellationToken);
    }

    private async Task<DeliveryRecord> WaitForRefusedRecordAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var tenantContext = new TenantContext();
            tenantContext.Set(tenantId);
            var dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(fixture.PostgreSql.GetConnectionString())
                .Options;
            await using var dbContext = new NotificationDbContext(dbOptions, tenantContext);
            var record = await dbContext.DeliveryRecords
                .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
            if (record?.Status == DeliveryStatus.Refused)
            {
                return record;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException($"Delivery record for request {requestId} was not refused.");
    }

    private sealed class FakeTransactionalEmailSender : ITransactionalEmailSender
    {
        public ConcurrentQueue<TransactionalEmail> SentEmails { get; } = new();

        public Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken)
        {
            SentEmails.Enqueue(email);
            return Task.CompletedTask;
        }
    }
}
