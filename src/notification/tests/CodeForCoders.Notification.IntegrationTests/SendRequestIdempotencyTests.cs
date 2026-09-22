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
public sealed class SendRequestIdempotencyTests(NotificationIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = nameof(RedeliveryOfTheSameRequestDoesNotDeliverTwice))]
    public async Task RedeliveryOfTheSameRequestDoesNotDeliverTwice()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var request = CreateRequest(tenantId, Guid.CreateVersion7());
        var emailSender = new FakeTransactionalEmailSender();
        var queue = CreateQueueName();
        using var host = CreateHost(emailSender, queue);

        await host.StartAsync(cancellationToken);
        try
        {
            var connectionProvider = host.Services.GetRequiredService<RabbitMqConnectionProvider>();
            await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);

            await PublishRequestAsync(channel, request, cancellationToken);
            await WaitForDeliveredRecordsAsync(
                tenantId,
                [request.PedidoId],
                cancellationToken);

            await PublishRequestAsync(channel, request, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            var records = await ReadDeliveryRecordsAsync(
                tenantId,
                [request.PedidoId],
                cancellationToken);
            Assert.Single(records);
            Assert.Equal(DeliveryStatus.Delivered, records[0].Status);
            Assert.Single(emailSender.SentEmails);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact(DisplayName = nameof(NewRequestIdDeliversAgainForTheSameRecipient))]
    public async Task NewRequestIdDeliversAgainForTheSameRecipient()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var firstRequest = CreateRequest(tenantId, Guid.CreateVersion7());
        var secondRequest = CreateRequest(tenantId, Guid.CreateVersion7());
        var emailSender = new FakeTransactionalEmailSender();
        var queue = CreateQueueName();
        using var host = CreateHost(emailSender, queue);

        await host.StartAsync(cancellationToken);
        try
        {
            var connectionProvider = host.Services.GetRequiredService<RabbitMqConnectionProvider>();
            await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);

            await PublishRequestAsync(channel, firstRequest, cancellationToken);
            await PublishRequestAsync(channel, secondRequest, cancellationToken);

            var records = await WaitForDeliveredRecordsAsync(
                tenantId,
                [firstRequest.PedidoId, secondRequest.PedidoId],
                cancellationToken);

            Assert.Equal(2, records.Count);
            Assert.Equal(2, emailSender.SentEmails.Count);
            Assert.NotEqual(records[0].Id, records[1].Id);
            Assert.All(records, record => Assert.Equal(DeliveryStatus.Delivered, record.Status));
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private IHost CreateHost(FakeTransactionalEmailSender emailSender, string queue)
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

    private static string CreateQueueName()
        => $"notification.integration.send-request.{Guid.CreateVersion7():N}";

    private static async Task PublishRequestAsync(
        IChannel channel,
        NotificationSendRequestedV1 request,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = $"integration-idempotency-{request.PedidoId:N}",
        };
        await channel.BasicPublishAsync(
            exchange: "notification.integration.events",
            routingKey: "notificacao.envio-solicitado.v1",
            mandatory: true,
            basicProperties: properties,
            body: JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions),
            cancellationToken: cancellationToken);
    }

    private async Task<IReadOnlyList<DeliveryRecord>> WaitForDeliveredRecordsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> requestIds,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var records = await ReadDeliveryRecordsAsync(tenantId, requestIds, cancellationToken);
            if (records.Count == requestIds.Count
                && records.All(record => record.Status == DeliveryStatus.Delivered))
            {
                return records;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException("The expected delivery records were not delivered.");
    }

    private async Task<IReadOnlyList<DeliveryRecord>> ReadDeliveryRecordsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> requestIds,
        CancellationToken cancellationToken)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);
        var dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new NotificationDbContext(dbOptions, tenantContext);
        return await dbContext.DeliveryRecords
            .Where(record => requestIds.Contains(record.RequestId))
            .OrderBy(record => record.RequestId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
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
