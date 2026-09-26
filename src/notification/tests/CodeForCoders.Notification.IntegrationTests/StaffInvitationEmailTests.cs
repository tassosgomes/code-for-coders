using System.Text;
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
public sealed class StaffInvitationEmailTests(NotificationIntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string processingNamespace = $"it-{Guid.CreateVersion7():N}";
    private string Exchange => RabbitMqResourceNames.Compose("notification.integration.events", processingNamespace);

    [Fact(DisplayName = nameof(StaffInvitationWithRoleIsAcceptedAndDeliveredWithRoleLinkAndValidity))]
    [Trait("Layer", "Notification staff invitation - Integration")]
    public async Task StaffInvitationWithRoleIsAcceptedAndDeliveredWithRoleLinkAndValidity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        const string recipient = "guest@example.com";
        const string role = "professor";
        const string link = "https://backoffice.example.invalid/admin/convite?token=invite123";
        var request = new NotificationSendRequestedV1(
            requestId,
            tenantId,
            recipient,
            NotificationPurposes.StaffInvitation,
            NotificationPurposes.StaffInvitation,
            new NotificationTemplateDataV1(null, link, role),
            DateTimeOffset.UtcNow);
        var sender = new FakeTransactionalEmailSender();
        var queue = $"notification.integration.invitation.{Guid.CreateVersion7():N}";
        using var host = CreateHost(sender, queue);

        await host.StartAsync(cancellationToken);
        try
        {
            var provider = host.Services.GetRequiredService<RabbitMqConnectionProvider>();
            await using var channel = await provider.CreateChannelAsync(cancellationToken);
            await PublishRequestAsync(channel, Exchange, request, cancellationToken);

            var email = await sender.SentEmail.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            Assert.Equal(recipient, email.To);
            Assert.Equal("Convite para acessar o backoffice da Code4Coders", email.Subject);
            Assert.Contains("como professor", email.TextBody, StringComparison.Ordinal);
            Assert.Contains(link, email.TextBody, StringComparison.Ordinal);
            Assert.Contains("O link vale por 168 horas e só funciona uma vez.", email.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain(recipient, email.TextBody, StringComparison.Ordinal);
            Assert.Contains("como professor", email.HtmlBody!, StringComparison.Ordinal);
            Assert.Contains(link, email.HtmlBody!, StringComparison.Ordinal);

            var record = await WaitForDeliveryRecordAsync(tenantId, requestId, cancellationToken);
            Assert.Equal(DeliveryStatus.Delivered, record.Status);
            Assert.Null(record.RecipientName);
            Assert.Equal(role, record.RecipientRole);
            Assert.Null(record.Link);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact(DisplayName = nameof(StaffInvitationWithoutRoleIsRefusedAsMissingData))]
    [Trait("Layer", "Notification staff invitation - Integration")]
    public void StaffInvitationWithoutRoleIsRefusedAsMissingData()
    {
        var request = CreateInvitationRequest(new NotificationTemplateDataV1(
            Nome: null,
            Link: "https://backoffice.example.invalid/admin/convite?token=invite123",
            Papel: null));

        Assert.Equal(
            NotificationRefusalReasons.MissingData,
            NotificationSendRequestRules.GetRefusalReason(request));
    }

    [Fact(DisplayName = nameof(ExistingNotificationModelsRemainAcceptedWithoutInvitationRole))]
    [Trait("Layer", "Notification staff invitation - Integration")]
    public void ExistingNotificationModelsRemainAcceptedWithoutInvitationRole()
    {
        var confirmation = new NotificationSendRequestedV1(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "student@example.com",
            NotificationPurposes.AccountConfirmation,
            NotificationPurposes.AccountConfirmation,
            new NotificationTemplateDataV1("Ana Souza", "https://accounts.example.invalid/confirm?token=abc"),
            DateTimeOffset.UtcNow);
        var recovery = new NotificationSendRequestedV1(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "student@example.com",
            NotificationPurposes.PasswordRecovery,
            NotificationPurposes.PasswordRecovery,
            new NotificationTemplateDataV1("Ana Souza", "https://accounts.example.invalid/reset?token=abc"),
            DateTimeOffset.UtcNow);

        Assert.Null(NotificationSendRequestRules.GetRefusalReason(confirmation));
        Assert.Null(NotificationSendRequestRules.GetRefusalReason(recovery));
        var oldContractJson = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(confirmation, JsonOptions));
        Assert.DoesNotContain("papel", oldContractJson, StringComparison.Ordinal);
        Assert.Contains("nome", oldContractJson, StringComparison.Ordinal);
    }

    private IHost CreateHost(FakeTransactionalEmailSender sender, string queue)
    {
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
            ["RabbitMq:HeartbeatQueue"] = $"notification.integration.heartbeat.{Guid.CreateVersion7():N}",
            ["RabbitMq:SendRequestQueue"] = queue,
            ["RabbitMq:SendRequestRoutingKey"] = "notificacao.envio-solicitado.v1",
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Outbox:BatchSize"] = "10",
            ["Outbox:MaxAttempts"] = "3",
            ["Delivery:PollingIntervalSeconds"] = "1",
            ["Email:SendingDomain"] = "example.invalid",
            ["Email:ValidityHoursByPurpose:confirmacao-de-conta"] = "24",
            ["Email:ValidityHoursByPurpose:recuperacao-de-senha"] = "1",
            ["Email:ValidityHoursByPurpose:convite-interno"] = "168",
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
                services.AddSingleton<ITransactionalEmailSender>(sender);
            })
            .Build();
    }

    private static NotificationSendRequestedV1 CreateInvitationRequest(NotificationTemplateDataV1 data)
        => new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "guest@example.com",
            NotificationPurposes.StaffInvitation,
            NotificationPurposes.StaffInvitation,
            data,
            DateTimeOffset.UtcNow);

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
            CorrelationId = $"integration-invitation-{request.PedidoId:N}",
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
            var dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(fixture.PostgreSql.GetConnectionString())
                .Options;
            await using var dbContext = new NotificationDbContext(dbOptions, tenantContext);
            var record = await dbContext.DeliveryRecords
                .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);
            if (record?.Status == DeliveryStatus.Delivered)
            {
                return record;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new Xunit.Sdk.XunitException($"Delivery record for request {requestId} was not delivered.");
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
