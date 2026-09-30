using CodeForCoders.Learning.Infra.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed class RabbitMqTopologyInitializer(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
        await channel.ExchangeDeclareAsync(
            settings.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            settings.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var deadLetterQueue = $"{settings.HeartbeatQueue}.dlq";
        await channel.QueueDeclareAsync(
            deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            deadLetterQueue,
            settings.DeadLetterExchange,
            settings.HeartbeatQueue,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            settings.HeartbeatQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = settings.HeartbeatQueue,
                ["x-delivery-limit"] = settings.DeliveryLimit,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            settings.HeartbeatQueue,
            settings.Exchange,
            "learning.platform.heartbeat.v1",
            arguments: null,
            cancellationToken: cancellationToken);
        await DeclareVideoFactsAsync(channel, settings, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task DeclareVideoFactsAsync(IChannel channel, RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(settings.MediaExchange, ExchangeType.Topic, true, false, cancellationToken: cancellationToken);
        var dlq = settings.VideoFactsQueue + ".dlq";
        await channel.QueueDeclareAsync(dlq, true, false, false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(dlq, settings.DeadLetterExchange, settings.VideoFactsQueue, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(settings.VideoFactsQueue, true, false, false, new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = settings.VideoFactsQueue,
            ["x-delivery-limit"] = settings.DeliveryLimit,
        }, cancellationToken: cancellationToken);
        foreach (var route in new[] { VideoAvailabilityFact.ReadyRoute, VideoAvailabilityFact.FailedRoute })
            await channel.QueueBindAsync(settings.VideoFactsQueue, settings.MediaExchange, route, cancellationToken: cancellationToken);
    }
}
