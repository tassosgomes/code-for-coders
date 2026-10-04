using CodeForCoders.Media.Application;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.Platform.RecordPlatformHeartbeat;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(MediaIntegrationCollection.Name)]
public sealed class MediaInfrastructureTests(MediaIntegrationFixture fixture)
{
    [Fact]
    public async Task AuditQueueReceivesBothMediaEventsWithoutNoRoute()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var suffix = Guid.CreateVersion7().ToString("N");
        var settings = Options.Create(new RabbitMqOptions
        {
            Host = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            Username = "code_for_coders",
            Password = "code_for_coders",
            Exchange = $"media.integration.audit.{suffix}",
            DeadLetterExchange = $"media.integration.audit.dlx.{suffix}",
            HeartbeatQueue = $"media.integration.heartbeat.{suffix}",
            AuditQueue = $"media.integration.events.audit.{suffix}",
            CoursePublicationsQueue = $"media.integration.course-publications.{suffix}",
        });
        await using var connection = new RabbitMqConnectionProvider(settings);
        var topology = new RabbitMqTopologyInitializer(connection, settings);
        await topology.StartAsync(cancellationToken);
        var publisher = new RabbitMqPublisher(connection, settings);
        var routingKeys = new[] { "midia.ativo-pronto.v1", "midia.preparacao-falhou.v1" };
        foreach (var routingKey in routingKeys)
        {
            var draft = new OutboxMessageDraft(Guid.CreateVersion7(), Guid.CreateVersion7(), routingKey, routingKey, new { }, DateTimeOffset.UtcNow, null);
            await publisher.PublishAsync(OutboxMessage.Create(draft, "{}"), cancellationToken);
        }

        await using var channel = await connection.CreateChannelAsync(cancellationToken);
        var received = new List<string>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (received.Count < 2 && DateTimeOffset.UtcNow < deadline)
        {
            var delivery = await channel.BasicGetAsync(settings.Value.AuditQueue, autoAck: true, cancellationToken);
            if (delivery is null)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            received.Add(delivery.RoutingKey);
        }

        Assert.Equal(routingKeys.Order(), received.Order());
    }

    [Fact]
    public async Task HeartbeatFlowsThroughOutboxRabbitMqAndConsumer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["Media:Role"] = "api",
            ["Preparation:MasterKey"] = Convert.ToBase64String(new byte[32]),
            ["Preparation:MasterKeyId"] = "media-test",
            ["Playback:Delivery:SharedSecret"] = "development-test-secret",
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = "media.integration.events",
            ["RabbitMq:DeadLetterExchange"] = "media.integration.events.dlx",
            ["RabbitMq:HeartbeatQueue"] = "media.integration.platform-heartbeat",
            ["RabbitMq:CoursePublicationsQueue"] = "media.integration.course-publications",
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
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await AssertProcessedAsync(dbContext, eventId, cancellationToken);
        }

        await host.StopAsync(cancellationToken);
    }

    private static async Task AssertProcessedAsync(
        MediaDbContext dbContext,
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
