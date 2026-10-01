using System.Diagnostics;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
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

public sealed class CatalogCourseConsumerWorker(RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options, ILogger<CatalogCourseConsumerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connectionProvider.CreateChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, options.Value.PrefetchCount, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var fact = PublishedCourseFact.Parse(delivery.Body);
                using var activity = CommerceTelemetry.ActivitySource.StartActivity("commerce.catalog.course.consume", ActivityKind.Consumer);
                await using var scope = scopes.CreateAsyncScope();
                var applied = await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(fact, stoppingToken);
                (applied ? CommerceTelemetry.CourseFactsApplied : CommerceTelemetry.CourseFactsIgnored).Add(1);
                CommerceTelemetry.CourseFactLag.Record(Math.Max(0, (DateTimeOffset.UtcNow - fact.PublishedAt).TotalSeconds));
                await channel.BasicAckAsync(delivery.DeliveryTag, false, CancellationToken.None);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                logger.LogWarning("Invalid published course fact sent to the dead-letter queue.");
                CommerceTelemetry.CourseFactsDeadLettered.Add(1);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, CancellationToken.None);
            }
            catch (Exception exception) when (exception is NpgsqlException or DbUpdateException)
            {
                logger.LogWarning("Published course projection temporarily unavailable; delivery will be retried.");
                var deliveries = delivery.BasicProperties.Headers is not null
                    && delivery.BasicProperties.Headers.TryGetValue("x-delivery-count", out var count) ? Convert.ToInt64(count) : 0;
                var retry = deliveries + 1 < options.Value.DeliveryLimit;
                if (retry) await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, deliveries))), stoppingToken);
                if (!retry) CommerceTelemetry.CourseFactsDeadLettered.Add(1);
                // RabbitMQ 4.3 counts failed deliveries only for reject; nack is an explicit return.
                await channel.BasicRejectAsync(delivery.DeliveryTag, retry, CancellationToken.None);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        };
        await channel.BasicConsumeAsync(options.Value.CatalogCourseQueue, false, consumer, stoppingToken);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
