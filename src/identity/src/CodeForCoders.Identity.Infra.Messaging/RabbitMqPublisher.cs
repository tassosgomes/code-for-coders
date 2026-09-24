using System.Text;
using CodeForCoders.Identity.Infra.Data.Outbox;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.Identity.Infra.Messaging;

public sealed class RabbitMqPublisher(
    RabbitMqConnectionProvider connectionProvider,
    OutboxPayloadProtector payloadProtector)
{
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(payloadProtector.Unprotect(message));
        try
        {
            await using var channel = await connectionProvider.CreatePublisherChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.Id.ToString(),
                Type = message.Type,
                CorrelationId = message.CorrelationId,
                Headers = BuildHeaders(message),
            };
            await channel.BasicPublishAsync(
                exchange: message.Exchange,
                routingKey: message.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
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

    private static Dictionary<string, object?>? BuildHeaders(OutboxMessage message)
    {
        var headers = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(message.TraceParent))
        {
            headers["traceparent"] = message.TraceParent;
        }

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            headers["correlationId"] = message.CorrelationId;
        }

        return headers.Count == 0 ? null : headers;
    }
}
