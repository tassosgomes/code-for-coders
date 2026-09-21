using System.Text;
using System.Text.Json;
using CodeForCoders.Audit.Application;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Infra.Data;
using CodeForCoders.Audit.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Audit.IntegrationTests;

[Collection(AuditIntegrationCollection.Name)]
public sealed class AuditEventConsumerIntegrationTests(AuditIntegrationFixture fixture)
{
    [Fact]
    public async Task ConsumedEventIsAppendedAndAcked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.PostgreSql.GetConnectionString(),
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = "audit.integration.events",
            ["RabbitMq:DeadLetterExchange"] = "audit.integration.events.dlx",
            ["RabbitMq:AuditQueue"] = "audit.integration.audit-event",
            ["RabbitMq:EventRoutingKey"] = "audit.audit.event.v1",
            ["AuditDatabase:WriterRole"] = "code_for_coders_audit_writer",
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

        var auditEvent = new AuditEventV1(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            "media",
            "MediaSmokeV1",
            "{}");
        var receiptStore = host.Services.GetRequiredService<AuditReceiptStore>();
        var receipt = receiptStore.Register(auditEvent.EventId);
        var connectionFactory = new ConnectionFactory
        {
            HostName = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            UserName = "code_for_coders",
            Password = "code_for_coders",
        };
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = auditEvent.EventId.ToString(),
            Type = "AuditEventV1",
        };
        await channel.BasicPublishAsync(
            "audit.integration.events",
            "audit.audit.event.v1",
            mandatory: true,
            properties,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(auditEvent)),
            cancellationToken);

        var consumed = await receipt.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        Assert.Equal(auditEvent.EventId, consumed.EventId);

        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var stored = await dbContext.AuditRecords.SingleAsync(
            record => record.Id == auditEvent.EventId,
            cancellationToken);
        Assert.Equal("media", stored.SourceService);
        Assert.Equal("MediaSmokeV1", stored.EventType);

        await host.StopAsync(cancellationToken);
    }
}
