using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Contracts;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.Notification.Infra.Messaging;

public sealed class NotificationSendRequestConsumerWorker(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationSendRequestConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(
            0,
            options.Value.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);
        var consumer = new SendRequestConsumer(channel, scopeFactory, logger);
        await channel.BasicConsumeAsync(
            queue: options.Value.SendRequestQueue,
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

    private sealed class SendRequestConsumer : AsyncDefaultBasicConsumer
    {
        private readonly IChannel channel;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<NotificationSendRequestConsumerWorker> logger;

        public SendRequestConsumer(
            IChannel channel,
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationSendRequestConsumerWorker> logger) : base(channel)
        {
            this.channel = channel;
            this.scopeFactory = scopeFactory;
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
                var request = JsonSerializer.Deserialize<NotificationSendRequestedV1>(
                    body.Span,
                    JsonOptions);
                ValidateEnvelope(request);

                using var activity = StartConsumerActivity(properties, routingKey, request!.PedidoId);
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<INotificationSendRequestMessageHandler>();
                await handler.HandleAsync(
                    request,
                    properties.CorrelationId,
                    cancellationToken);
                await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Invalid notification send request sent to the dead-letter queue.");
                await channel.BasicNackAsync(
                    deliveryTag,
                    multiple: false,
                    requeue: false,
                    CancellationToken.None);
            }
            catch (DbUpdateException exception) when (IsDeliveryRecordUniqueViolation(exception))
            {
                logger.LogInformation(
                    "Notification send request was already processed; acknowledging the redelivery.");
                await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
            }
            catch (AlreadyClosedException exception)
            {
                logger.LogError(exception, "RabbitMQ channel closed while consuming a notification request.");
            }
        }

        private static bool IsDeliveryRecordUniqueViolation(DbUpdateException exception)
            => exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            }
                && exception.Entries.Any(entry => entry.Entity is DeliveryRecord);

        private static void ValidateEnvelope(NotificationSendRequestedV1? request)
        {
            if (request is null
                || request.PedidoId == Guid.Empty
                || request.TenantId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.Destinatario)
                || request.Destinatario.Length > DeliveryRecord.RecipientMaxLength)
            {
                throw new JsonException("The notification send request payload is invalid.");
            }
        }

        private static Activity? StartConsumerActivity(
            IReadOnlyBasicProperties properties,
            string routingKey,
            Guid requestId)
        {
            var traceParent = properties.Headers is not null
                && properties.Headers.TryGetValue("traceparent", out var value)
                ? value switch
                {
                    byte[] bytes => Encoding.UTF8.GetString(bytes),
                    string text => text,
                    _ => null,
                }
                : null;
            var parent = traceParent is not null
                && ActivityContext.TryParse(traceParent, null, out var parentContext)
                ? parentContext
                : default;
            return RabbitMqTelemetry.ActivitySource.StartActivity(
                "notification.send-request.consume",
                ActivityKind.Consumer,
                parent,
                new ActivityTagsCollection
                {
                    ["messaging.rabbitmq.routing_key"] = routingKey,
                    ["messaging.message.id"] = requestId.ToString(),
                });
        }
    }
}
