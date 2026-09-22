using CodeForCoders.Notification.Application;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.UseCases.Platform.RecordPlatformHeartbeat;
using CodeForCoders.Notification.Infra.Data;
using CodeForCoders.Notification.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodeForCoders.Notification.IntegrationTests;

[Collection(NotificationIntegrationCollection.Name)]
public sealed class NotificationInfrastructureTests(NotificationIntegrationFixture fixture)
{
    [Fact]
    public async Task HeartbeatFlowsThroughOutboxRabbitMqAndConsumer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = "notification.integration.events",
            ["RabbitMq:DeadLetterExchange"] = "notification.integration.events.dlx",
            ["RabbitMq:HeartbeatQueue"] = "notification.integration.platform-heartbeat",
            ["Outbox:PollingIntervalSeconds"] = "5",
            ["Outbox:BatchSize"] = "10",
            ["Outbox:MaxAttempts"] = "3",
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
            })
            .Build();

        await host.StartAsync(cancellationToken);

        Guid eventId;
        Guid tenantId;
        using (var scope = host.Services.CreateScope())
        {
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantId = Guid.CreateVersion7();
            tenantContext.Set(tenantId);
            var useCase = scope.ServiceProvider.GetRequiredService<IRecordPlatformHeartbeat>();
            var output = await useCase.ExecuteAsync(
                new RecordPlatformHeartbeatInput(tenantId),
                cancellationToken);
            eventId = output.EventId;
        }

        var receiptStore = host.Services.GetRequiredService<HeartbeatReceiptStore>();
        var heartbeat = await receiptStore.Register(eventId)
            .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        Assert.Equal(eventId, heartbeat.EventId);
        Assert.Equal(tenantId, heartbeat.TenantId);

        using (var scope = host.Services.CreateScope())
        {
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.Set(tenantId);
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            await AssertProcessedAsync(dbContext, eventId, cancellationToken);
        }

        await host.StopAsync(cancellationToken);
    }

    private static async Task AssertProcessedAsync(
        NotificationDbContext dbContext,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var processed = await dbContext.OutboxMessages
                .SingleOrDefaultAsync(message => message.Id == eventId, cancellationToken);
            if (processed?.ProcessedOn is not null)
            {
                Assert.Equal(0, processed.Attempts);
                return;
            }

            dbContext.ChangeTracker.Clear();
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        Assert.Fail($"Outbox message {eventId} was not processed before the timeout.");
    }
}
