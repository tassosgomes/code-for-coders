using System.Diagnostics;
using System.Text.Json;
using CodeForCoders.Learning.Application.Common;
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

public sealed class PlaybackProgressConsumerWorker(RabbitMqConnectionProvider connections, IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options, TimeProvider clock, ILogger<PlaybackProgressConsumerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connections.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => ConsumeAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(options.Value.PlaybackProgressQueue, false, consumer, stoppingToken);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ConsumeAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        Guid? eventId = Guid.TryParse(delivery.BasicProperties.MessageId, out var id) ? id : null;
        try
        {
            var fact = PlaybackProgressFact.Parse(delivery.Body, delivery.RoutingKey, delivery.BasicProperties.MessageId);
            using var activity = LearningTelemetry.ActivitySource.StartActivity("learning.playback-progress.consume", ActivityKind.Consumer);
            await ApplyWithRetryAsync(fact, cancellationToken);
            LearningTelemetry.PlaybackProgressConsumed.Add(1);
            LearningTelemetry.PlaybackProgressLag.Record(Math.Max(0, (clock.GetUtcNow() - fact.OccurredAt).TotalSeconds));
            logger.LogInformation("Playback progress fact {EventId} consumed.", fact.EventId);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, CancellationToken.None);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException || IsDatabaseFailure(exception))
        {
            var retry = IsTransient(exception) && !DeliveryLimitReached(delivery);
            logger.LogWarning("Playback progress fact {EventId} delivery failed ({FailureType}); requeue {Requeue}.", eventId, exception.GetType().Name, retry);
            if (!retry) LearningTelemetry.PlaybackProgressDeadLettered.Add(1);
            await channel.BasicNackAsync(delivery.DeliveryTag, false, retry, CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task ApplyWithRetryAsync(PlaybackProgressFact fact, CancellationToken cancellationToken)
    {
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<PlaybackProgressStore>().ApplyAsync(fact, cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < attempts && IsTransient(exception))
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1) + Random.Shared.NextDouble()), cancellationToken);
            }
        }
    }

    private static bool IsDatabaseFailure(Exception exception) => exception is NpgsqlException or DbUpdateException or TimeoutException;
    private bool DeliveryLimitReached(BasicDeliverEventArgs delivery)
        => delivery.BasicProperties.Headers?.TryGetValue("x-delivery-count", out var value) == true
            && value is long count && count >= options.Value.DeliveryLimit;

    private static bool IsTransient(Exception exception) => exception is NpgsqlException { IsTransient: true } or TimeoutException
        || exception is DbUpdateException { InnerException: NpgsqlException { IsTransient: true } };
}
