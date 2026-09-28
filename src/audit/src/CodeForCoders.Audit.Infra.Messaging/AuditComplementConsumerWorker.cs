using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed class AuditComplementConsumerWorker(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<AuditComplementConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, global: false, cancellationToken: stoppingToken);
        var consumer = new AuditComplementConsumer(channel, scopeFactory, options.Value, logger);
        await channel.BasicConsumeAsync(
            queue: options.Value.AuditComplementQueue,
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

    private sealed class AuditComplementConsumer : AsyncDefaultBasicConsumer
    {
        private readonly IChannel channel;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly RabbitMqOptions options;
        private readonly ILogger<AuditComplementConsumerWorker> logger;

        public AuditComplementConsumer(
            IChannel channel,
            IServiceScopeFactory scopeFactory,
            RabbitMqOptions options,
            ILogger<AuditComplementConsumerWorker> logger) : base(channel)
        {
            this.channel = channel;
            this.scopeFactory = scopeFactory;
            this.options = options;
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
            if (!TryDeserializeComplement(body, out var complement, out var reason)
                || routingKey != options.AuditComplementEventRoutingKey
                || properties.Type != "ComplementoConfirmado"
                || !Guid.TryParse(properties.MessageId, out var messageId)
                || messageId != complement!.ConfirmationId)
            {
                AuditTelemetry.MessagesIllegible.Add(1);
                logger.LogError(
                    "Unreadable audit complement sent to the dead-letter queue {Reason}",
                    reason ?? "message-metadata");
                await TryNackAsync(deliveryTag, requeue: false);
                return;
            }

            try
            {
                using var activity = StartConsumerActivity(properties);
                await using var scope = scopeFactory.CreateAsyncScope();
                var recorder = scope.ServiceProvider.GetRequiredService<IAuditComplementRecorder>();
                var result = await recorder.RecordAsync(complement, CancellationToken.None);
                if (result is AuditComplementRecordStatus.Rejected)
                {
                    await TryNackAsync(deliveryTag, requeue: false);
                    return;
                }

                await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
            {
                logger.LogError(
                    exception,
                    "Unexpected failure while recording an audit complement; the message will be retried.");
                await TryNackAsync(deliveryTag, requeue: true);
            }
        }

        private async Task TryNackAsync(ulong deliveryTag, bool requeue)
        {
            try
            {
                if (requeue)
                {
                    // basic.reject marks this delivery failed so RabbitMQ 4.3 applies x-delivery-limit.
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
                logger.LogError(exception, "RabbitMQ channel closed while rejecting an audit complement.");
            }
        }

        private static bool TryDeserializeComplement(
            ReadOnlyMemory<byte> body,
            [NotNullWhen(true)] out ComplementoConfirmadoV1? complement,
            [NotNullWhen(false)] out string? reason)
        {
            try
            {
                complement = JsonSerializer.Deserialize<ComplementoConfirmadoV1>(body.Span, SerializerOptions);
            }
            catch (JsonException)
            {
                complement = null;
                reason = "body";
                return false;
            }

            if (complement is null)
            {
                reason = "body";
                return false;
            }

            if (complement.ConfirmationId == Guid.Empty)
            {
                reason = "confirmationId";
                return false;
            }

            if (complement.TenantId == Guid.Empty)
            {
                reason = "tenantId";
                return false;
            }

            if (complement.OriginalRecordId == Guid.Empty)
            {
                reason = "originalRecordId";
                return false;
            }

            if (complement.ConfirmedAt == default)
            {
                reason = "confirmedAt";
                return false;
            }

            if (complement.Author is null || complement.Author.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(complement.Author.Type)
                || complement.Author.Type.Length > 100)
            {
                reason = "author";
                return false;
            }

            if (string.IsNullOrWhiteSpace(complement.Explanation) || complement.Explanation.Length > 1000)
            {
                reason = "explanation";
                return false;
            }

            reason = null;
            return true;
        }

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

            return AuditTelemetry.ActivitySource.StartActivity(
                "audit.complements.consume",
                ActivityKind.Consumer,
                parent);
        }
    }
}
