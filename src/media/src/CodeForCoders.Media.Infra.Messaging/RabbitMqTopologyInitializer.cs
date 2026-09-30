using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CodeForCoders.Media.Infra.Messaging;

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

        await DeclareCoursePublicationsAsync(channel, settings, cancellationToken);

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
            "media.platform.heartbeat.v1",
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            settings.AuditQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-message-ttl"] = settings.AuditMessageTtlMilliseconds,
                ["x-max-length"] = settings.AuditMaxLength,
                ["x-overflow"] = "drop-head",
            },
            cancellationToken: cancellationToken);
        foreach (var routingKey in new[] { "midia.ativo-pronto.v1", "midia.preparacao-falhou.v1" })
        {
            await channel.QueueBindAsync(
                settings.AuditQueue,
                settings.Exchange,
                routingKey,
                arguments: null,
                cancellationToken: cancellationToken);
        }
    }

    private static async Task DeclareCoursePublicationsAsync(IChannel channel, RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(settings.LearningExchange, ExchangeType.Topic, true, false, cancellationToken: cancellationToken);
        var queue = settings.CoursePublicationsQueue;
        await channel.QueueDeclareAsync(queue + ".dlq", true, false, false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue + ".dlq", settings.DeadLetterExchange, queue, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queue, true, false, false, new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = queue,
            ["x-delivery-limit"] = settings.DeliveryLimit,
        }, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, settings.LearningExchange, PublishedCourseFact.Route, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
