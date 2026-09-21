using System.Diagnostics;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Contracts;
using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.Commerce.Infra.Messaging;

public sealed class HeartbeatConsumerWorker(
    RabbitMqConnectionProvider connectionProvider,
    HeartbeatReceiptStore receiptStore,
    IOptions<RabbitMqOptions> options,
    ILogger<HeartbeatConsumerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, global: false, cancellationToken: stoppingToken);
        var consumer = new HeartbeatConsumer(channel, receiptStore, logger);
        await channel.BasicConsumeAsync(
            queue: options.Value.HeartbeatQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private sealed class HeartbeatConsumer : AsyncDefaultBasicConsumer
    {
        private readonly IChannel channel;
        private readonly HeartbeatReceiptStore receiptStore;
        private readonly ILogger<HeartbeatConsumerWorker> logger;

        public HeartbeatConsumer(
            IChannel channel,
            HeartbeatReceiptStore receiptStore,
            ILogger<HeartbeatConsumerWorker> logger) : base(channel)
        {
            this.channel = channel;
            this.receiptStore = receiptStore;
            this.logger = logger;
        }

        public override async Task HandleBasicDeliverAsync(
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var heartbeat = JsonSerializer.Deserialize<CommercePlatformHeartbeatV1>(
                    body.Span,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (heartbeat is null || heartbeat.EventId == Guid.Empty || heartbeat.TenantId == Guid.Empty)
                {
                    throw new JsonException("The platform heartbeat payload is invalid.");
                }

                using var activity = StartConsumerActivity(properties, routingKey, heartbeat.EventId);
                receiptStore.MarkConsumed(heartbeat);
                CommerceTelemetry.HeartbeatsConsumed.Add(1);
                await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Invalid platform heartbeat sent to the dead-letter queue.");
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, CancellationToken.None);
            }
            catch (AlreadyClosedException exception)
            {
                logger.LogError(exception, "RabbitMQ channel closed while consuming the platform heartbeat.");
            }
        }

        private static Activity? StartConsumerActivity(
            IReadOnlyBasicProperties properties,
            string routingKey,
            Guid eventId)
        {
            var traceParent = properties.Headers is not null
                && properties.Headers.TryGetValue("traceparent", out var value)
                ? value switch
                {
                    byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                    string text => text,
                    _ => null,
                }
                : null;
            var parent = traceParent is not null
                && ActivityContext.TryParse(traceParent, null, out var parentContext)
                ? parentContext
                : default;
            return RabbitMqTelemetry.ActivitySource.StartActivity(
                "commerce.platform.heartbeat.consume",
                ActivityKind.Consumer,
                parent,
                new ActivityTagsCollection
                {
                    ["messaging.rabbitmq.routing_key"] = routingKey,
                    ["messaging.message.id"] = eventId.ToString(),
                });
        }
    }
}
