using CodeForCoders.Notification.Infra.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CodeForCoders.Notification.Infra.Messaging;

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

        await DeclareQueueAsync(
            channel,
            settings,
            settings.HeartbeatQueue,
            "notification.platform.heartbeat.v1",
            cancellationToken);
        await DeclareQueueAsync(
            channel,
            settings,
            settings.SendRequestQueue,
            settings.SendRequestRoutingKey,
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task DeclareQueueAsync(
        IChannel channel,
        RabbitMqOptions settings,
        string queue,
        string routingKey,
        CancellationToken cancellationToken)
    {
        var deadLetterQueue = $"{queue}.dlq";
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
            queue,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue,
                ["x-delivery-limit"] = settings.DeliveryLimit,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue,
            settings.Exchange,
            routingKey,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
