using System.Diagnostics;
using System.Text.Json;
using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Infra.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.Audit.Infra.Messaging;

public sealed class AuditEventConsumerWorker(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    AuditReceiptStore receiptStore,
    IOptions<RabbitMqOptions> options,
    ILogger<AuditEventConsumerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, global: false, cancellationToken: stoppingToken);
        var consumer = new AuditEventConsumer(channel, scopeFactory, receiptStore, logger);
        await channel.BasicConsumeAsync(
            queue: options.Value.AuditQueue,
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

    private sealed class AuditEventConsumer : AsyncDefaultBasicConsumer
    {
        private readonly IChannel channel;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly AuditReceiptStore receiptStore;
        private readonly ILogger<AuditEventConsumerWorker> logger;

        public AuditEventConsumer(
            IChannel channel,
            IServiceScopeFactory scopeFactory,
            AuditReceiptStore receiptStore,
            ILogger<AuditEventConsumerWorker> logger) : base(channel)
        {
            this.channel = channel;
            this.scopeFactory = scopeFactory;
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
                var auditEvent = JsonSerializer.Deserialize<AuditEventV1>(
                    body.Span,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (auditEvent is null || auditEvent.EventId == Guid.Empty || auditEvent.TenantId == Guid.Empty)
                {
                    throw new JsonException("The audit event payload is invalid.");
                }

                using var activity = StartConsumerActivity(properties, routingKey, auditEvent.EventId);
                await using var scope = scopeFactory.CreateAsyncScope();
                var recorder = scope.ServiceProvider.GetRequiredService<IAuditEventRecorder>();
                await recorder.RecordAsync(auditEvent, CancellationToken.None);
                receiptStore.MarkConsumed(auditEvent);
                AuditTelemetry.EventsConsumed.Add(1);
                await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Invalid audit event sent to the dead-letter queue.");
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, CancellationToken.None);
            }
            catch (AlreadyClosedException exception)
            {
                logger.LogError(exception, "RabbitMQ channel closed while consuming an audit event.");
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
                "audit.events.consume",
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
