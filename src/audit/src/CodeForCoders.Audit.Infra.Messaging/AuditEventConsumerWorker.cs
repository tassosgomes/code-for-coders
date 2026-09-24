using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            try
            {
                var act = JsonSerializer.Deserialize<AtoPraticado>(body.Span, SerializerOptions);
                if (act is null
                    || act.FatoId == Guid.Empty
                    || act.TenantId == Guid.Empty
                    || string.IsNullOrWhiteSpace(act.Origem))
                {
                    throw new JsonException("The administrative act envelope is invalid.");
                }

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
                await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "Invalid administrative act sent to the dead-letter queue.");
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, CancellationToken.None);
            }
            catch (AlreadyClosedException exception)
            {
                logger.LogError(exception, "RabbitMQ channel closed while consuming an administrative act.");
            }
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

            return RabbitMqTelemetry.ActivitySource.StartActivity(
                "audit.acts.consume",
                ActivityKind.Consumer,
                parent);
        }
    }
}
