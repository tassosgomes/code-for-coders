using CodeForCoders.Commerce.Application;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.Platform.RecordPlatformHeartbeat;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class CommerceInfrastructureTests(CommerceIntegrationFixture fixture)
{
    [Fact]
    public async Task HeartbeatFlowsThroughOutboxRabbitMqAndConsumer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connection = new NpgsqlConnectionStringBuilder(fixture.PostgreSql.GetConnectionString());
        var dbName = $"infra_{Guid.CreateVersion7():N}";
        await using (var admin = new NpgsqlConnection(connection.ConnectionString))
        {
            await admin.OpenAsync(cancellationToken);
            await using var command = admin.CreateCommand();
            command.CommandText = $"CREATE DATABASE {dbName}";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        connection.Database = dbName;
        var isolatedConnectionString = connection.ConnectionString;
        await using (var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(isolatedConnectionString).Options, new TenantContext()))
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = isolatedConnectionString,
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = "commerce.integration.events",
            ["RabbitMq:DeadLetterExchange"] = "commerce.integration.events.dlx",
            ["RabbitMq:AuditExchange"] = "commerce.integration.audit.events",
            ["RabbitMq:LearningExchange"] = "commerce.integration.learning.events",
            ["RabbitMq:BillingExchange"] = "commerce.integration.billing.events",
            ["RabbitMq:CatalogCourseQueue"] = "commerce.integration.catalog-course",
            ["RabbitMq:EntitlementCourseQueue"] = "commerce.integration.entitlement-course",
            ["RabbitMq:HeartbeatQueue"] = "commerce.integration.platform-heartbeat",
            ["RabbitMq:EntitlementFactRetentionQueue"] = "commerce.integration.entitlement-fact-retention",
            ["RabbitMq:OfferRetentionQueue"] = "commerce.integration.catalog-offer-retention",
            ["RabbitMq:SalesPaymentsQueue"] = "commerce.integration.sales-payments",
            ["RabbitMq:EntitlementPurchasesQueue"] = "commerce.integration.entitlement-purchases",
            ["RabbitMq:SalesAccessGrantedQueue"] = "commerce.integration.sales-access-granted",
            ["Outbox:PollingIntervalSeconds"] = "1",
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
            .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        Assert.Equal(eventId, heartbeat.EventId);
        Assert.Equal(tenantId, heartbeat.TenantId);

        using (var scope = host.Services.CreateScope())
        {
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.Set(tenantId);
            var dbContext = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            await AssertProcessedAsync(dbContext, eventId, cancellationToken);
        }

        await host.StopAsync(cancellationToken);
    }

    private static async Task AssertProcessedAsync(
        CommerceDbContext dbContext,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
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
