using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Health;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoFactReplayCollection.Name)]
[Trait("Layer", "Retained outbox fact - Integration")]
public sealed class RetainedOutboxFactTests(VideoFactReplayFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private MediaDbContext Context() => new(
        new DbContextOptionsBuilder<MediaDbContext>().UseNpgsql(fixture.Database.GetConnectionString()).Options,
        new TenantContext());

    [Fact(DisplayName = nameof(RetainedFactIsBornProcessedAndOutboxHealthCheckRemainsHealthy))]
    public async Task RetainedFactIsBornProcessedAndOutboxHealthCheckRemainsHealthy()
    {
        var tenant = Guid.CreateVersion7();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Outbox:RetainedRoutingKeys:0"] = "midia.reproducao-avancou.v1"
            })
            .Build();

        await using var context = Context();
        var writer = new OutboxMessageWriter(context, Options.Create(new OutboxOptions
        {
            RetainedRoutingKeys = ["midia.reproducao-avancou.v1"]
        }));

        var eventId = PlaybackSession.CreateDeterministicEventId(Guid.CreateVersion7(), 1);
        var payload = new
        {
            eventId,
            tenantId = tenant,
            sessionId = Guid.CreateVersion7(),
            studentId = Guid.CreateVersion7(),
            courseId = Guid.CreateVersion7(),
            lessonId = Guid.CreateVersion7(),
            sequence = 1,
            positionSeconds = 30,
            reason = "heartbeat",
            occurredAt = DateTimeOffset.UtcNow.ToString("O")
        };

        var draft = new OutboxMessageDraft(
            eventId,
            tenant,
            "midia.reproducao-avancou.v1",
            "midia.reproducao-avancou.v1",
            payload,
            DateTimeOffset.UtcNow,
            "00-traceparent-01");

        await writer.AppendAsync(draft, Cancellation);
        await context.SaveChangesAsync(Cancellation);

        var saved = await context.OutboxMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == eventId, Cancellation);
        Assert.NotNull(saved.ProcessedOn);

        var healthCheck = new OutboxHealthCheck(context);
        var health = await healthCheck.CheckHealthAsync(new HealthCheckContext(), Cancellation);
        Assert.Equal(HealthStatus.Healthy, health.Status);
    }

    [Fact(DisplayName = nameof(ReplayProgressFactsPublishesRetainedFactsInOrderToSpecifiedQueue))]
    public async Task ReplayProgressFactsPublishesRetainedFactsInOrderToSpecifiedQueue()
    {
        var tenant = Guid.CreateVersion7();
        var session = Guid.CreateVersion7();
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var fact1 = await SeedProgressFactAsync(tenant, 1, 30, "heartbeat", session, t0);
        var fact2 = await SeedProgressFactAsync(tenant, 2, 60, "heartbeat", session, t0.AddSeconds(30));
        var fact3 = await SeedProgressFactAsync(tenant, 3, 90, "paused", session, t0.AddSeconds(60));

        await ReplayAndAssertAsync(tenant, [fact1, fact2, fact3], repetitions: 1);
    }

    [Fact(DisplayName = nameof(ReplayProgressFactsIsTenantScopedAndExcludesOtherTenants))]
    public async Task ReplayProgressFactsIsTenantScopedAndExcludesOtherTenants()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var factA = await SeedProgressFactAsync(tenantA, 1, 30, "heartbeat");
        await SeedProgressFactAsync(tenantB, 1, 30, "heartbeat");

        await ReplayAndAssertAsync(tenantA, [factA], repetitions: 1);
    }

    [Fact(DisplayName = nameof(RepeatedReplayDoesNotDuplicateEventsOrMutateOutboxRows))]
    public async Task RepeatedReplayDoesNotDuplicateEventsOrMutateOutboxRows()
    {
        var tenant = Guid.CreateVersion7();
        var fact = await SeedProgressFactAsync(tenant, 1, 30, "heartbeat");

        await ReplayAndAssertAsync(tenant, [fact], repetitions: 2);

        await using var context = Context();
        var retained = await context.OutboxMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == fact.Id, Cancellation);
        Assert.Equal(fact.ProcessedOn, retained.ProcessedOn);
        Assert.Equal(0, retained.Attempts);
        Assert.Equal(1, await context.OutboxMessages.IgnoreQueryFilters().CountAsync(m => m.TenantId == tenant, Cancellation));
    }

    [Fact(DisplayName = nameof(ReplayIncludesFactsFromSessionsEndedByDeniedDecision))]
    public async Task ReplayIncludesFactsFromSessionsEndedByDeniedDecision()
    {
        var tenant = Guid.CreateVersion7();
        var factEnded = await SeedProgressFactAsync(tenant, 1, 45, "paused");

        await ReplayAndAssertAsync(tenant, [factEnded], repetitions: 1);
    }

    [Fact(DisplayName = nameof(RemovingRoutingKeyFromRetainedListAllowsWorkerToPublishLive))]
    public async Task RemovingRoutingKeyFromRetainedListAllowsWorkerToPublishLive()
    {
        var tenant = Guid.CreateVersion7();
        await using var context = Context();
        var writer = new OutboxMessageWriter(context, Options.Create(new OutboxOptions()));

        var eventId = PlaybackSession.CreateDeterministicEventId(Guid.CreateVersion7(), 1);
        var draft = new OutboxMessageDraft(
            eventId,
            tenant,
            "midia.reproducao-avancou.v1",
            "midia.reproducao-avancou.v1",
            new { eventId },
            DateTimeOffset.UtcNow,
            null);

        await writer.AppendAsync(draft, Cancellation);
        await context.SaveChangesAsync(Cancellation);

        var saved = await context.OutboxMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == eventId, Cancellation);
        Assert.Null(saved.ProcessedOn); // Born unworked, live publishing ready
    }

    private async Task<OutboxMessage> SeedProgressFactAsync(
        Guid tenant,
        int sequence,
        int positionSeconds,
        string reason,
        Guid? sessionId = null,
        DateTimeOffset? occurredAt = null)
    {
        var sid = sessionId ?? Guid.CreateVersion7();
        var eventId = PlaybackSession.CreateDeterministicEventId(sid, sequence);
        var time = occurredAt ?? DateTimeOffset.UtcNow.AddMinutes(-5);
        var payload = JsonSerializer.Serialize(new
        {
            eventId,
            tenantId = tenant,
            sessionId = sid,
            studentId = Guid.CreateVersion7(),
            courseId = Guid.CreateVersion7(),
            lessonId = Guid.CreateVersion7(),
            sequence,
            positionSeconds,
            reason,
            occurredAt = time.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });

        var message = OutboxMessage.Create(
            new OutboxMessageDraft(eventId, tenant, "midia.reproducao-avancou.v1", "midia.reproducao-avancou.v1", new { }, time, "00-trace-01"),
            payload);
        message.MarkProcessed();

        await using var context = Context();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync(Cancellation);

        return await context.OutboxMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync(m => m.Id == eventId, Cancellation);
    }

    private async Task ReplayAndAssertAsync(Guid tenant, OutboxMessage[] expected, int repetitions)
    {
        var suffix = Guid.CreateVersion7().ToString("N");
        var exchange = "media.replay." + suffix;
        var queue = "learning.replay." + suffix;
        var options = Options.Create(new RabbitMqOptions
        {
            Host = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            Username = "replay-test",
            Password = "replay-test",
            Exchange = exchange,
        });

        await using var connection = new RabbitMqConnectionProvider(options);
        await using var channel = await connection.CreateChannelAsync(Cancellation);
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, true, false, cancellationToken: Cancellation);
        await channel.QueueDeclareAsync(queue, true, false, false, new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: Cancellation);
        await channel.QueueBindAsync(queue, exchange, "midia.#", cancellationToken: Cancellation);

        var services = new ServiceCollection();
        services.AddScoped(_ => Context());
        await using var provider = services.BuildServiceProvider();

        using var worker = new OutboxPublisherWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new RabbitMqPublisher(connection, options),
            Options.Create(new OutboxOptions { BatchSize = 1, ProgressFactReplayQueue = queue }),
            NullLogger<OutboxPublisherWorker>.Instance);

        for (var i = 0; i < repetitions; i++)
        {
            Assert.Equal(expected.Length, await worker.ReplayProgressFactsAsync(tenant, Cancellation));
        }

        for (var i = 0; i < repetitions; i++)
        {
            for (var j = 0; j < expected.Length; j++)
            {
                var delivery = await channel.BasicGetAsync(queue, true, Cancellation);
                Assert.NotNull(delivery);
                var original = expected[j];
                Assert.Equal(original.Id.ToString(), delivery.BasicProperties.MessageId);
                Assert.Equal(original.Payload, Encoding.UTF8.GetString(delivery.Body.Span));
                Assert.Equal("midia.reproducao-avancou.v1", delivery.RoutingKey);
                Assert.True(delivery.BasicProperties.Persistent);

                // Assert against asyncapi-contract.yaml: No email, no title, no videoId, no percentage
                using var doc = JsonDocument.Parse(delivery.Body);
                var root = doc.RootElement;
                Assert.True(root.TryGetProperty("eventId", out _));
                Assert.True(root.TryGetProperty("tenantId", out _));
                Assert.True(root.TryGetProperty("sessionId", out _));
                Assert.True(root.TryGetProperty("studentId", out _));
                Assert.True(root.TryGetProperty("courseId", out _));
                Assert.True(root.TryGetProperty("lessonId", out _));
                Assert.True(root.TryGetProperty("sequence", out _));
                Assert.True(root.TryGetProperty("positionSeconds", out _));
                Assert.True(root.TryGetProperty("reason", out _));
                Assert.True(root.TryGetProperty("occurredAt", out _));
                Assert.False(root.TryGetProperty("email", out _));
                Assert.False(root.TryGetProperty("title", out _));
                Assert.False(root.TryGetProperty("videoId", out _));
                Assert.False(root.TryGetProperty("percentage", out _));
            }
        }

        Assert.Null(await channel.BasicGetAsync(queue, true, Cancellation));
    }
}
