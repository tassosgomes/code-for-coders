using System.Net.Sockets;
using System.Text;
using CodeForCoders.BffAdmin.Infra.Data.Outbox;
using CodeForCoders.BffAdmin.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.BffAdmin.Infra.Messaging;

public sealed class RabbitMqPublisher(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options)
{
    public async Task PublishAsync(OutboxMessage message, string payload, CancellationToken cancellationToken)
    {
        try
        {
            await using var channel = await connectionProvider.CreatePublisherChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.Id.ToString(),
                Type = message.Type,
                Headers = string.IsNullOrWhiteSpace(message.TraceParent)
                    ? null
                    : new Dictionary<string, object?> { ["traceparent"] = message.TraceParent },
            };
            await channel.BasicPublishAsync(
                message.DestinationExchange ?? options.Value.Exchange,
                message.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(payload),
                cancellationToken);
        }
        catch (PublishException exception)
        {
            throw new OutboxPublishException("RabbitMQ rejected the outbox message.", exception);
        }
        catch (AlreadyClosedException exception)
        {
            throw new OutboxPublishException(
                "RabbitMQ channel closed while publishing the outbox message.",
                exception,
                brokerUnavailable: !IsChannelLevelRejection(exception.ShutdownReason));
        }
        catch (BrokerUnreachableException exception)
        {
            throw new OutboxPublishException(
                "RabbitMQ is unavailable while publishing the outbox message.",
                exception,
                brokerUnavailable: true);
        }
        catch (Exception exception) when (exception is IOException or SocketException or TimeoutException)
        {
            throw new OutboxPublishException(
                "RabbitMQ connection failed while publishing the outbox message.",
                exception,
                brokerUnavailable: true);
        }
    }

    // AMQP soft errors (4xx) close only the channel because of this message's destination; any other
    // shutdown means the transport itself went away and must not consume the message's attempts.
    private static bool IsChannelLevelRejection(ShutdownEventArgs? reason)
        => reason is { Initiator: ShutdownInitiator.Peer, ReplyCode: >= 400 and < 500 };
}
