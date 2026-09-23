using CodeForCoders.Identity.Application;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.UseCases.Platform.RecordPlatformHeartbeat;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class IdentityInfrastructureTests(IdentityIntegrationFixture fixture)
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
            ["RabbitMq:Exchange"] = "identity.integration.events",
            ["RabbitMq:NotificationExchange"] = "notification.integration.events.default",
            ["RabbitMq:DeadLetterExchange"] = "identity.integration.events.dlx",
            ["RabbitMq:HeartbeatQueue"] = "identity.integration.platform-heartbeat",
            ["Outbox:PollingIntervalSeconds"] = "5",
            ["Outbox:BatchSize"] = "10",
            ["Outbox:MaxAttempts"] = "3",
            ["Valkey:ConnectionString"] = "localhost:6379,abortConnect=false",
            ["StudentAccount:ConfirmationBaseUrl"] = "https://students.example.test/confirm-account",
            ["StudentAccount:ConfirmationLifetimeHours"] = "24",
            ["StudentAccount:PasswordResetBaseUrl"] = "https://students.example.test/redefinir-senha",
            ["StudentAccount:PasswordResetLifetimeHours"] = "1",
            ["Idempotency:FingerprintKeyBase64"] = Convert.ToBase64String(new byte[32]),
            ["OutboxProtection:KeyBase64"] = Convert.ToBase64String(new byte[32]),
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
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await AssertProcessedAsync(dbContext, eventId, cancellationToken);
        }

        await host.StopAsync(cancellationToken);
    }

    private static async Task AssertProcessedAsync(
        IdentityDbContext dbContext,
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
