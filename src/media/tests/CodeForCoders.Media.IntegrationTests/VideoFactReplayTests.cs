using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoFactReplayCollection.Name)]
public sealed class VideoFactReplayTests(VideoFactReplayFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private MediaDbContext Context() => new(new DbContextOptionsBuilder<MediaDbContext>().UseNpgsql(fixture.Database.GetConnectionString()).Options, new TenantContext());

    [Fact(DisplayName = nameof(HistoricalReadyAndFailedFactsPreserveOriginalMessageIdPayloadAndDurability))]
    public async Task HistoricalReadyAndFailedFactsPreserveOriginalMessageIdPayloadAndDurability()
    {
        var tenant = Guid.CreateVersion7(); var ready = await SeedAsync(tenant, "midia.ativo-pronto.v1"); var failed = await SeedAsync(tenant, "midia.preparacao-falhou.v1");
        await ReplayAndAssertAsync(tenant, [ready, failed], 1);
    }

    [Fact(DisplayName = nameof(ReplayIsTenantScopedAndExcludesHeartbeatFacts))]
    public async Task ReplayIsTenantScopedAndExcludesHeartbeatFacts()
    {
        var tenant = Guid.CreateVersion7(); var ready = await SeedAsync(tenant, "midia.ativo-pronto.v1");
        await SeedAsync(Guid.CreateVersion7(), "midia.ativo-pronto.v1"); await SeedAsync(tenant, "media.platform.heartbeat.v1");
        await ReplayAndAssertAsync(tenant, [ready], 1);
    }

    [Fact(DisplayName = nameof(RepeatedReplayDoesNotResetLiveOutboxOrGenerateNewEvents))]
    public async Task RepeatedReplayDoesNotResetLiveOutboxOrGenerateNewEvents()
    {
        var tenant = Guid.CreateVersion7(); var ready = await SeedAsync(tenant, "midia.ativo-pronto.v1");
        await ReplayAndAssertAsync(tenant, [ready], 2);
        await using var context = Context();
        var retained = await context.OutboxMessages.IgnoreQueryFilters().SingleAsync(message => message.Id == ready.Id, Cancellation);
        Assert.Equal(ready.ProcessedOn, retained.ProcessedOn); Assert.Equal(0, retained.Attempts);
        Assert.Equal(1, await context.OutboxMessages.IgnoreQueryFilters().CountAsync(message => message.TenantId == tenant, Cancellation));
    }

    private async Task<OutboxMessage> SeedAsync(Guid tenant, string route)
    {
        var id = Guid.CreateVersion7(); var occurredAt = DateTimeOffset.UtcNow.AddDays(-2);
        var payload = JsonSerializer.Serialize(new { eventId = id, tenantId = tenant, videoId = Guid.CreateVersion7(), occurredAt, durationSeconds = 120, reason = "invalid-video" });
        var message = OutboxMessage.Create(new OutboxMessageDraft(id, tenant, route, route, new { }, occurredAt, null), payload);
        message.MarkProcessed();
        await using var context = Context(); context.OutboxMessages.Add(message); await context.SaveChangesAsync(Cancellation);
        return await context.OutboxMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync(item => item.Id == id, Cancellation);
    }

    private async Task ReplayAndAssertAsync(Guid tenant, OutboxMessage[] expected, int repetitions)
    {
        var suffix = Guid.CreateVersion7().ToString("N"); var exchange = "media.replay." + suffix; var queue = "learning.replay." + suffix;
        var options = Options.Create(new RabbitMqOptions { Host = fixture.RabbitMq.Hostname, Port = fixture.RabbitMq.GetMappedPublicPort(5672), Username = "replay-test", Password = "replay-test", Exchange = exchange });
        await using var connection = new RabbitMqConnectionProvider(options);
        await using var channel = await connection.CreateChannelAsync(Cancellation);
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, true, false, cancellationToken: Cancellation);
        await channel.QueueDeclareAsync(queue, true, false, false, new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: Cancellation);
        await channel.QueueBindAsync(queue, exchange, "midia.#", cancellationToken: Cancellation);
        var services = new ServiceCollection(); services.AddScoped(_ => Context());
        await using var provider = services.BuildServiceProvider();
        using var worker = new OutboxPublisherWorker(provider.GetRequiredService<IServiceScopeFactory>(), new RabbitMqPublisher(connection, options), Options.Create(new OutboxOptions { BatchSize = 1, VideoFactReplayQueue = queue }), NullLogger<OutboxPublisherWorker>.Instance);
        for (var index = 0; index < repetitions; index++) Assert.Equal(expected.Length, await worker.ReplayVideoFactsAsync(tenant, Cancellation));
        for (var index = 0; index < repetitions * expected.Length; index++)
        {
            var delivery = await channel.BasicGetAsync(queue, true, Cancellation); Assert.NotNull(delivery);
            var original = Assert.Single(expected, message => message.Id.ToString() == delivery.BasicProperties.MessageId);
            Assert.Equal(original.Payload, Encoding.UTF8.GetString(delivery.Body.Span));
            Assert.Equal(original.RoutingKey, delivery.RoutingKey); Assert.True(delivery.BasicProperties.Persistent);
        }
        Assert.Null(await channel.BasicGetAsync(queue, true, Cancellation));
    }
}
