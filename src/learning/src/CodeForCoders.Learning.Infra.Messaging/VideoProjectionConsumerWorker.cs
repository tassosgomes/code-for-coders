using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using CodeForCoders.Learning.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed class VideoProjectionConsumerWorker(
    RabbitMqConnectionProvider connectionProvider, IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options, ILogger<VideoProjectionConsumerWorker> logger) : BackgroundService
{
    private static readonly Meter Meter = new("CodeForCoders.Learning.VideoProjection");
    private static readonly Counter<long> Consumed = Meter.CreateCounter<long>("learning.video_facts.consumed");
    private static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("learning.video_facts.dead_lettered");
    private static readonly Histogram<double> Lag = Meter.CreateHistogram<double>("learning.video_facts.lag", "s");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var fact = VideoAvailabilityFact.Parse(delivery.Body, delivery.RoutingKey, delivery.BasicProperties.MessageId);
                using var activity = RabbitMqTelemetry.ActivitySource.StartActivity("learning.video-fact.consume", ActivityKind.Consumer);
                await ApplyWithRetryAsync(fact, stoppingToken);
                Consumed.Add(1);
                Lag.Record(Math.Max(0, (DateTimeOffset.UtcNow - fact.OccurredAt).TotalSeconds));
                await channel.BasicAckAsync(delivery.DeliveryTag, false, CancellationToken.None);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or DbUpdateException or NpgsqlException)
            {
                logger.LogError(exception, "Video availability fact sent to the dead-letter queue.");
                DeadLettered.Add(1);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, CancellationToken.None);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        };
        await channel.BasicConsumeAsync(options.Value.VideoFactsQueue, false, consumer, stoppingToken);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ApplyWithRetryAsync(VideoAvailabilityFact fact, CancellationToken cancellationToken)
    {
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<VideoProjectionStore>().ApplyAsync(fact, cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < attempts && exception is NpgsqlException or DbUpdateException)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1) + Random.Shared.NextDouble()), cancellationToken);
            }
        }
    }
}
