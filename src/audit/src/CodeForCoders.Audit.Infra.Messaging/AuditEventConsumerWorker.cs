using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Application.Common;
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
    IOptions<RabbitMqOptions> options,
    ILogger<AuditEventConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, global: false, cancellationToken: stoppingToken);
        var consumer = new AuditActConsumer(channel, scopeFactory, logger);
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

    private sealed class AuditActConsumer : AsyncDefaultBasicConsumer
    {
        private readonly IChannel channel;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<AuditEventConsumerWorker> logger;

        public AuditActConsumer(
            IChannel channel,
            IServiceScopeFactory scopeFactory,
            ILogger<AuditEventConsumerWorker> logger) : base(channel)
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
            if (!TryDeserializeAct(body, out var act, out var reason))
            {
                AuditTelemetry.MessagesIllegible.Add(
                    1,
                    new KeyValuePair<string, object?>("reason", reason));
                logger.LogError(
                    "Unreadable administrative act sent to the dead-letter queue. Reason {Reason}",
                    reason);
                await TryNackAsync(deliveryTag, requeue: false);
                return;
            }

            try
            {
                using var activity = StartConsumerActivity(properties);
                activity?.SetTag("fatoId", act.FatoId);
                activity?.SetTag("origem", act.Origem);
                activity?.SetTag("tipo", act.Tipo);
                activity?.SetTag("tenantId", act.TenantId);

                await using var scope = scopeFactory.CreateAsyncScope();
                var recorder = scope.ServiceProvider.GetRequiredService<IAuditActRecorder>();
                await recorder.RecordAsync(act, CancellationToken.None);

                logger.LogInformation(
                    "Administrative act recorded {FatoId} {Origem} {Tipo} {TenantId}",
                    act.FatoId,
                    act.Origem,
                    act.Tipo,
                    act.TenantId);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
            {
                logger.LogError(
                    exception,
                    "Unexpected failure while recording an administrative act; the message will be retried.");
                await TryNackAsync(deliveryTag, requeue: true);
                return;
            }

            try
            {
                await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
            }
            catch (AlreadyClosedException exception)
            {
                logger.LogError(exception, "RabbitMQ channel closed while acknowledging an administrative act.");
            }
        }

        private async Task TryNackAsync(ulong deliveryTag, bool requeue)
        {
            try
            {
                if (requeue)
                {
                    // RabbitMQ 4.3 does not count basic.nack requeues toward x-delivery-limit.
                    await channel.BasicRejectAsync(deliveryTag, requeue: true, CancellationToken.None);
                }
                else
                {
                    await channel.BasicNackAsync(
                        deliveryTag,
                        multiple: false,
                        requeue: false,
                        CancellationToken.None);
                }
            }
            catch (AlreadyClosedException exception)
            {
                logger.LogError(exception, "RabbitMQ channel closed while rejecting an administrative act.");
            }
        }

        private static bool TryDeserializeAct(
            ReadOnlyMemory<byte> body,
            [NotNullWhen(true)] out AtoPraticado? act,
            [NotNullWhen(false)] out string? reason)
        {
            try
            {
                act = JsonSerializer.Deserialize<AtoPraticado>(body.Span, SerializerOptions);
            }
            catch (JsonException)
            {
                act = null;
                reason = "body";
                return false;
            }

            if (act is null)
            {
                reason = "body";
                return false;
            }

            if (act.FatoId == Guid.Empty)
            {
                reason = "fatoId";
                return false;
            }

            if (!IsContractOrigin(act.Origem))
            {
                reason = "origem";
                return false;
            }

            if (act.TenantId == Guid.Empty)
            {
                reason = "tenantId";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsContractOrigin(string? origin)
            => origin is { Length: > 0 and <= 100 }
                && origin[0] is >= 'a' and <= 'z'
                && origin.All(static character => character is >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '-');

        private static Activity? StartConsumerActivity(IReadOnlyBasicProperties properties)
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
                "audit.acts.consume",
                ActivityKind.Consumer,
                parent);
        }
    }
}
