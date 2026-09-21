using CodeForCoders.Audit.Infra.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CodeForCoders.Audit.Infra.Messaging;

public sealed class AuditTopologyInitializer(
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

        var deadLetterQueue = $"{settings.AuditQueue}.dlq";
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
            settings.AuditQueue,
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
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = settings.AuditQueue,
                ["x-delivery-limit"] = settings.DeliveryLimit,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            settings.AuditQueue,
            settings.Exchange,
            settings.EventRoutingKey,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
