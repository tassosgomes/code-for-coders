using System.Text;
using System.Text.Json;
using CodeForCoders.ContractTesting;
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

[Collection(ProgressFactPublicationCollection.Name)]
[Trait("Layer", "Progress fact publication - Integration")]
public sealed class ProgressFactPublicationTests(VideoFactReplayFixture fixture)
{
    private const string Route = "midia.reproducao-avancou.v1";
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private MediaDbContext Context() => new(new DbContextOptionsBuilder<MediaDbContext>()
        .UseNpgsql(fixture.Database.GetConnectionString()).Options, new TenantContext());

    [Fact(DisplayName = nameof(ConsumerQueueExistsBeforeRetentionIsDisabledAndLiveFactIsPublished))]
    public Task ConsumerQueueExistsBeforeRetentionIsDisabledAndLiveFactIsPublished() => VerifyPublicationAsync("live");

    [Fact(DisplayName = nameof(ReplayPublishesRetainedRowsWithOriginalEventIdentity))]
    public Task ReplayPublishesRetainedRowsWithOriginalEventIdentity() => VerifyPublicationAsync("replay");

    [Fact(DisplayName = nameof(ExplicitRetentionPreventsLivePublicationEvenWithConsumerQueueBound))]
    public Task ExplicitRetentionPreventsLivePublicationEvenWithConsumerQueueBound() => VerifyPublicationAsync("retained");

    private async Task VerifyPublicationAsync(string scenario)
    {
        var suffix = Guid.CreateVersion7().ToString("N");
        var tenant = Guid.CreateVersion7(); var eventId = Guid.CreateVersion7();
        var settings = Options.Create(new RabbitMqOptions
        {
            Host = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            Username = "replay-test",
            Password = "replay-test",
            Exchange = "media.progress." + suffix,
        });
        var queue = "learning.progress." + suffix;
        await using var connection = new RabbitMqConnectionProvider(settings);
        await using var channel = await connection.CreateChannelAsync(Cancellation);
        await channel.ExchangeDeclareAsync(settings.Value.Exchange, ExchangeType.Topic, true, false, cancellationToken: Cancellation);
        await channel.QueueDeclareAsync(queue, true, false, false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: Cancellation);
        await channel.QueueBindAsync(queue, settings.Value.Exchange, Route, cancellationToken: Cancellation);
        var outbox = Options.Create(new OutboxOptions
        {
            PollingIntervalSeconds = 1,
            ProgressFactReplayQueue = queue,
            RetainedRoutingKeys = scenario == "live" ? [] : [Route],
        });
        var payload = new
        {
            eventId,
            tenantId = tenant,
            sessionId = Guid.CreateVersion7(),
            studentId = Guid.CreateVersion7(),
            courseId = Guid.CreateVersion7(),
            lessonId = Guid.CreateVersion7(),
            sequence = 1,
            positionSeconds = 252,
            reason = "paused",
            occurredAt = DateTimeOffset.UtcNow
        };
        var draft = new OutboxMessageDraft(eventId, tenant, Route, Route, payload, payload.occurredAt, null);
        string persistedPayload;
        await using (var db = Context())
        {
            await new OutboxMessageWriter(db, outbox).AppendAsync(draft, Cancellation);
            await db.SaveChangesAsync(Cancellation);
            var saved = await db.OutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == eventId, Cancellation);
            Assert.Equal(scenario != "live", saved.ProcessedOn.HasValue);
            persistedPayload = await db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == eventId).Select(item => item.Payload).SingleAsync(Cancellation);
        }
        var services = new ServiceCollection(); services.AddScoped(_ => Context());
        await using var provider = services.BuildServiceProvider();
        using var worker = new OutboxPublisherWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            new RabbitMqPublisher(connection, settings), outbox, NullLogger<OutboxPublisherWorker>.Instance);
        if (scenario == "replay")
        {
            for (var repetition = 0; repetition < 2; repetition++)
            {
                Assert.Equal(1, await worker.ReplayProgressFactsAsync(tenant, Cancellation));
                var delivery = await channel.BasicGetAsync(queue, true, Cancellation);
                AssertDelivery(delivery, eventId, persistedPayload);
            }
        }
        else
        {
            await worker.StartAsync(Cancellation);
            try
            {
                if (scenario == "live")
                {
                    BasicGetResult? delivery = null;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(15));
                    while (delivery is null)
                    {
                        delivery = await channel.BasicGetAsync(queue, true, timeout.Token);
                        if (delivery is null) await Task.Delay(50, timeout.Token);
                    }
                    AssertDelivery(delivery, eventId, persistedPayload);
                }
                else
                {
                    // Let at least two outbox polls pass to prove the retained row never reaches the broker.
                    await Task.Delay(TimeSpan.FromSeconds(3), Cancellation);
                    Assert.Null(await channel.BasicGetAsync(queue, true, Cancellation));
                }
            }
            finally { await worker.StopAsync(Cancellation); }
        }
        await using var context = Context();
        var row = await context.OutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == eventId, Cancellation);
        Assert.NotNull(row.ProcessedOn); Assert.Equal(0, row.Attempts);
        Assert.Equal(1, await context.OutboxMessages.IgnoreQueryFilters().CountAsync(item => item.TenantId == tenant, Cancellation));
    }

    private static void AssertDelivery(BasicGetResult? delivery, Guid eventId, string persistedPayload)
    {
        Assert.NotNull(delivery); Assert.Equal(eventId.ToString(), delivery.BasicProperties.MessageId);
        var json = Encoding.UTF8.GetString(delivery.Body.Span);
        Assert.Equal(persistedPayload, json);
        Assert.Equal(Route, delivery.RoutingKey); Assert.True(delivery.BasicProperties.Persistent);
        AsyncApiContract.Load("media/asyncapi.yaml").AssertSends(Route, json);
    }
}
