using System.Diagnostics;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
namespace CodeForCoders.Commerce.Infra.Messaging;

public sealed class SalesPaymentConsumerWorker(RabbitMqConnectionProvider connectionProvider, IServiceScopeFactory scopes,
 IOptions<RabbitMqOptions> options, ILogger<SalesPaymentConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                ActivityContext.TryParse(delivery.BasicProperties.CorrelationId, null, out var parent);
                using var activity = CommerceTelemetry.ActivitySource.StartActivity("commerce.purchase.consume", ActivityKind.Consumer, parent);
                await using var scope = scopes.CreateAsyncScope();
                var sink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
                if (delivery.RoutingKey == "cobranca.pagamento-aguardando.v1")
                {
                    var fact = JsonSerializer.Deserialize<PaymentAwaitingFact>(delivery.Body.Span, JsonOptions)
                        ?? throw new JsonException("Empty purchase flow fact.");
                    await sink.ApplyAwaitingAsync(fact, stoppingToken);
                }
                else
                {
                    var fact = JsonSerializer.Deserialize<PaymentConfirmedFact>(delivery.Body.Span, JsonOptions)
                        ?? throw new JsonException("Empty purchase flow fact.");
                    await sink.ApplyAsync(fact, stoppingToken);
                }
                await channel.BasicAckAsync(delivery.DeliveryTag, false, CancellationToken.None);
            }
            catch (Exception error) when (error is JsonException or OrderRuleException or EntitlementRuleException)
            {
                logger.LogError("Invalid purchase flow fact sent to the dead-letter queue."); CommerceTelemetry.PurchaseDeadLetters.Add(1);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, CancellationToken.None);
            }
            catch (Exception error) when (error is NpgsqlException or DbUpdateException)
            {
                var count = delivery.BasicProperties.Headers is not null && delivery.BasicProperties.Headers.TryGetValue("x-delivery-count", out var value)
           ? Convert.ToInt64(value) : 0;
                var retry = count + 1 < options.Value.DeliveryLimit;
                if (retry) await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, count))), stoppingToken);
                if (!retry) { CommerceTelemetry.PurchaseDeadLetters.Add(1); logger.LogError("Purchase flow retries exhausted; delivery sent to the dead-letter queue."); }
                await channel.BasicRejectAsync(delivery.DeliveryTag, retry, CancellationToken.None);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        };
        await channel.BasicConsumeAsync(options.Value.SalesPaymentsQueue, false, consumer, stoppingToken);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
