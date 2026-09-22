using CodeForCoders.Notification.Infra.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace CodeForCoders.Notification.Infra.Messaging;

public sealed class RabbitMqTopologyInitializer(
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqResourceNames resourceNames) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
        await channel.ExchangeDeclareAsync(
            resourceNames.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            resourceNames.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await DeclareQueueAsync(
            channel,
            resourceNames,
            resourceNames.HeartbeatQueue,
            "notification.platform.heartbeat.v1",
            cancellationToken);
        await DeclareQueueAsync(
            channel,
            resourceNames,
            resourceNames.SendRequestQueue,
            resourceNames.SendRequestRoutingKey,
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task DeclareQueueAsync(
        IChannel channel,
        RabbitMqResourceNames resourceNames,
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
            resourceNames.DeadLetterExchange,
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
                ["x-dead-letter-exchange"] = resourceNames.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue,
                ["x-delivery-limit"] = resourceNames.DeliveryLimit,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue,
            resourceNames.Exchange,
            routingKey,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
