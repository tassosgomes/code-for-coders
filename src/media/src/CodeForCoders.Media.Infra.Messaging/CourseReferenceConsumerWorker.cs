using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class CourseReferenceConsumerWorker(
    RabbitMqConnectionProvider connectionProvider, IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options, ILogger<CourseReferenceConsumerWorker> logger) : BackgroundService
{
    private static readonly Meter Meter = new("CodeForCoders.Media.CourseReferences");
    private static readonly Counter<long> Consumed = Meter.CreateCounter<long>("media.course_references.consumed");
    private static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("media.course_references.dead_lettered");
    private static readonly Histogram<double> Lag = Meter.CreateHistogram<double>("media.course_references.lag", "s");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var fact = PublishedCourseFact.Parse(delivery.Body, delivery.RoutingKey, delivery.BasicProperties.MessageId);
                using var activity = RabbitMqTelemetry.ActivitySource.StartActivity("media.course-reference.consume", ActivityKind.Consumer);
                await ApplyWithRetryAsync(fact, stoppingToken);
                Consumed.Add(1);
                Lag.Record(Math.Max(0, (DateTimeOffset.UtcNow - fact.PublishedAt).TotalSeconds));
                await channel.BasicAckAsync(delivery.DeliveryTag, false, CancellationToken.None);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or DbUpdateException or NpgsqlException)
            {
                logger.LogError("Course publication fact sent to the dead-letter queue ({ErrorType}).", exception.GetType().Name);
                DeadLettered.Add(1);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, CancellationToken.None);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        };
        await channel.BasicConsumeAsync(options.Value.CoursePublicationsQueue, false, consumer, stoppingToken);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ApplyWithRetryAsync(PublishedCourseFact fact, CancellationToken cancellationToken)
    {
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<CourseReferenceStore>().ApplyAsync(fact, cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < attempts && exception is NpgsqlException or DbUpdateException)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1) + Random.Shared.NextDouble()), cancellationToken);
            }
        }
    }
}
