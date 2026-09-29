using System.Diagnostics.Metrics;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class RabbitMqDeadLetterMetricsWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(60);

    private readonly RabbitMqManagementClient managementClient;
    private readonly string queueName;
    private readonly ILogger<RabbitMqDeadLetterMetricsWorker> logger;
    private QueueSnapshot? snapshot;

    public RabbitMqDeadLetterMetricsWorker(
        RabbitMqManagementClient managementClient,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        ILogger<RabbitMqDeadLetterMetricsWorker> logger)
    {
        this.managementClient = managementClient;
        this.logger = logger;
        var options = rabbitMqOptions.Value;
        queueName = $"{options.HeartbeatQueue}.dlq";

        MediaTelemetry.Meter.CreateObservableGauge<long>(
            "media.messaging.dlq.messages",
            ObserveQueueDepth,
            unit: "{message}");
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var messageCount = await managementClient.GetDeadLetterQueueMessageCountAsync(cancellationToken);
        Volatile.Write(ref snapshot, new QueueSnapshot(messageCount));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);
        try
        {
            do
            {
                try
                {
                    await RefreshAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (HttpRequestException exception)
                {
                    MarkUnavailable(exception);
                }
                catch (JsonException exception)
                {
                    MarkUnavailable(exception);
                }
                catch (TimeoutRejectedException exception)
                {
                    MarkUnavailable(exception);
                }
                catch (OperationCanceledException exception)
                {
                    MarkUnavailable(exception);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private IEnumerable<Measurement<long>> ObserveQueueDepth()
    {
        var current = Volatile.Read(ref snapshot);
        return current is null
            ? []
            : [new Measurement<long>(current.MessageCount, new KeyValuePair<string, object?>("queue", queueName))];
    }

    private void MarkUnavailable(Exception exception)
    {
        Volatile.Write(ref snapshot, null);
        logger.LogWarning(exception, "RabbitMQ dead-letter queue metrics collection failed.");
    }

    private sealed record QueueSnapshot(long MessageCount);
}
