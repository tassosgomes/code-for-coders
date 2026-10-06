using System.Diagnostics;
using System.Text.Json;
using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Contracts;
using CodeForCoders.Billing.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.Billing.Infra.Messaging;

public sealed class OrderCancellationConsumerWorker(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    RabbitMqResourceNames resourceNames,
    IOptions<RabbitMqOptions> options,
    ILogger<OrderCancellationConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, global: false, cancellationToken: stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<OrderCancelledV1>(delivery.Body.Span, JsonOptions)
                    ?? throw new JsonException("The order cancelled payload is empty.");
                if (message.EventId == Guid.Empty || message.TenantId == Guid.Empty || message.OrderId == Guid.Empty)
                {
                    throw new JsonException("The order cancelled payload is invalid.");
                }

                using var activity = StartConsumerActivity(delivery.BasicProperties, delivery.RoutingKey, message.EventId);

                using var scope = scopeFactory.CreateScope();
                var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                var store = scope.ServiceProvider.GetRequiredService<IPaymentStore>();
                var gateway = scope.ServiceProvider.GetRequiredService<IPaymentGateway>();
                var outbox = scope.ServiceProvider.GetRequiredService<IOutboxMessageWriter>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                tenant.Set(message.TenantId);
                await using var transaction = await store.LockAsync(message.TenantId, message.OrderId, stoppingToken);
                var payment = await store.FindAsync(message.OrderId, stoppingToken);

                if (payment is null || payment.ConfirmedAt is not null || payment.Status == "confirmed")
                {
                    // Confirmation prevails (RN-V08) or payment never opened; nothing to cancel.
                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
                    return;
                }

                if (payment.Status == "not-confirmed")
                {
                    // Already terminal (cancelled or expired); idempotent repeat, the reason is kept.
                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
                    return;
                }

                // A gateway failure propagates: the message is requeued and, once the delivery limit is exhausted,
                // dead-lettered. The payment is never recorded as cancelled while the gateway is unreachable. A permanent
                // refusal (page or PIX no longer cancellable) still records it: a late confirmation prevails (RN-V08).
                if (!string.IsNullOrEmpty(payment.SessionReference))
                {
                    await gateway.ExpireSessionAsync(payment.SessionReference, stoppingToken);
                }

                if (!string.IsNullOrEmpty(payment.GatewayReference))
                {
                    await gateway.CancelPaymentIntentAsync(payment.GatewayReference, stoppingToken);
                }

                if (payment.MarkNotConfirmed("cancelled"))
                {
                    var eventId = Guid.CreateVersion7();
                    var now = clock.GetUtcNow();
                    var fact = new PaymentNotConfirmedV1(eventId, message.TenantId, payment.Id, message.OrderId, "cancelled", now);
                    await outbox.AppendAsync(new(eventId, message.TenantId, "PagamentoNaoConfirmado", "cobranca.pagamento-nao-confirmado.v1", fact,
                        now, Activity.Current?.Id, Activity.Current?.Id ?? eventId.ToString("D")), stoppingToken);
                    await unitOfWork.CommitAsync(stoppingToken);
                }

                await transaction.CompleteAsync(stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Invalid order cancellation payload sent to the dead-letter queue.");
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            }
            catch (AlreadyClosedException exception)
            {
                logger.LogError(exception, "RabbitMQ channel closed while consuming the order cancellation.");
            }
            catch (Exception exception) when (exception is NpgsqlException or DbUpdateException or GatewayUnavailableException)
            {
                var deliveries = delivery.BasicProperties.Headers is not null
                    && delivery.BasicProperties.Headers.TryGetValue("x-delivery-count", out var count) ? Convert.ToInt64(count) : 0;
                var retry = deliveries + 1 < options.Value.DeliveryLimit;
                logger.LogError(exception, retry
                    ? "Transient error processing order cancellation for delivery tag {DeliveryTag}; delivery will be retried."
                    : "Transient error processing order cancellation for delivery tag {DeliveryTag}; retries exhausted, sent to the dead-letter queue.",
                    delivery.DeliveryTag);
                if (retry) await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, deliveries))), stoppingToken);
                // RabbitMQ 4.3 counts failed deliveries only for reject; nack is an explicit return.
                await channel.BasicRejectAsync(delivery.DeliveryTag, retry, CancellationToken.None);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        };

        await channel.BasicConsumeAsync(
            queue: resourceNames.OrderCancellationsQueue,
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
            "billing.order.cancellation.consume",
            ActivityKind.Consumer,
            parent,
            new ActivityTagsCollection
            {
                ["messaging.rabbitmq.routing_key"] = routingKey,
                ["messaging.message.id"] = eventId.ToString(),
            });
    }
}
