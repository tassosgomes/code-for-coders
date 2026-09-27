using System.Text;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class RabbitMqPublisher(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options)
{
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await using var channel = await connectionProvider.CreatePublisherChannelAsync(cancellationToken);
            var messageId = message.Id.ToString("N");
            var correlationId = message.TraceParent ?? $"00-{messageId}-{messageId[..16]}-01";
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.Id.ToString(),
                Type = message.Type,
                CorrelationId = correlationId,
                Headers = new Dictionary<string, object?>
                {
                    ["correlationId"] = correlationId,
                    ["traceparent"] = correlationId,
                },
            };
            await channel.BasicPublishAsync(
                exchange: options.Value.Exchange,
                routingKey: message.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken);
        }
        catch (PublishException exception)
        {
            throw new OutboxPublishException("RabbitMQ rejected the outbox message.", exception);
        }
        catch (AlreadyClosedException exception)
        {
            throw new OutboxPublishException("RabbitMQ channel closed while publishing the outbox message.", exception);
        }
    }
}
