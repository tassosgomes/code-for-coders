using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(MediaIntegrationCollection.Name)]
public sealed class MediaOutboxMetricsTests(MediaIntegrationFixture fixture)
{
    private const string ActiveReadyRoutingKey = "midia.ativo-pronto.v1";

    [Fact(DisplayName = nameof(OutboxPublicationRecordsPublishedEvent))]
    [Trait("Layer", "Media outbox metrics - Integration")]
    public async Task OutboxPublicationRecordsPublishedEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetOutboxAsync(cancellationToken);
        using var metrics = new MetricCapture();
        var rabbitOptions = CreateRabbitOptions();
        await using var services = CreateMessagingServices(rabbitOptions, maxAttempts: 3);
        await services.GetRequiredService<RabbitMqTopologyInitializer>().StartAsync(cancellationToken);
        var messageId = await SeedOutboxMessageAsync(ActiveReadyRoutingKey, DateTimeOffset.UtcNow, 0, cancellationToken);
        var worker = services.GetRequiredService<OutboxPublisherWorker>();

        await worker.StartAsync(cancellationToken);
        try
        {
            await WaitForOutboxStateAsync(messageId, (processedOn, _) => processedOn is not null, cancellationToken);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        var published = Assert.Single(metrics.Measurements("media.outbox.published"));
        Assert.Equal(1, published.Value);
        Assert.Equal("ativo-pronto", published.Event);
    }

    [Fact(DisplayName = nameof(RegisterFailureRecordsPublishFailedEvent))]
    [Trait("Layer", "Media outbox metrics - Integration")]
    public async Task RegisterFailureRecordsPublishFailedEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetOutboxAsync(cancellationToken);
        using var metrics = new MetricCapture();
        var rabbitOptions = CreateRabbitOptions();
        await using var services = CreateMessagingServices(rabbitOptions, maxAttempts: 1);
        var connection = services.GetRequiredService<RabbitMqConnectionProvider>();
        await using (var channel = await connection.CreateChannelAsync(cancellationToken))
        {
            await channel.ExchangeDeclareAsync(
                rabbitOptions.Exchange,
                ExchangeType.Topic,
                durable: true,
                cancellationToken: cancellationToken);
        }

        var messageId = await SeedOutboxMessageAsync(ActiveReadyRoutingKey, DateTimeOffset.UtcNow, 0, cancellationToken);
        var worker = services.GetRequiredService<OutboxPublisherWorker>();
        await worker.StartAsync(cancellationToken);
        try
        {
            await WaitForOutboxStateAsync(messageId, (_, attempts) => attempts == 1, cancellationToken);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        var failed = Assert.Single(metrics.Measurements("media.outbox.publish_failed"));
        Assert.Equal(1, failed.Value);
        Assert.Equal("ativo-pronto", failed.Event);
    }

    [Fact(DisplayName = nameof(SnapshotReportsPendingAgeAndExhaustedMessages))]
    [Trait("Layer", "Media outbox metrics - Integration")]
    public async Task SnapshotReportsPendingAgeAndExhaustedMessages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetOutboxAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await SeedOutboxMessageAsync(ActiveReadyRoutingKey, now.AddMinutes(-30), 0, cancellationToken);
        await SeedOutboxMessageAsync(ActiveReadyRoutingKey, now.AddMinutes(-10), 3, cancellationToken);
        await SeedOutboxMessageAsync(ActiveReadyRoutingKey, now.AddMinutes(-5), 0, cancellationToken);

        using var metrics = new MetricCapture();
        await using var services = CreateMetricsServices(maxAttempts: 3);
        var worker = services.GetRequiredService<MediaVolumeMetricsWorker>();
        await worker.RefreshAsync(cancellationToken);
        metrics.Observe();

        var pending = Assert.Single(metrics.Measurements("media.outbox.pending"), value => value.Value == 3);
        var oldest = Assert.Single(metrics.Measurements("media.outbox.oldest_pending"), value => value.Value >= 1790 && value.Value <= 1820);
        var exhausted = Assert.Single(metrics.Measurements("media.outbox.exhausted"), value => value.Value == 1);
        Assert.Empty(pending.Tags);
        Assert.Empty(oldest.Tags);
        Assert.Empty(exhausted.Tags);
    }

    [Fact(DisplayName = nameof(ApiRoleDoesNotRegisterOutboxGauges))]
    [Trait("Layer", "Media outbox metrics - Integration")]
    public void ApiRoleDoesNotRegisterOutboxGauges()
    {
        static IServiceCollection ServicesFor(string role)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Media:Role"] = role })
                .Build();
            return new ServiceCollection().AddMessagingConfiguration(configuration);
        }

        static bool IsWorkerGaugeService(ServiceDescriptor descriptor)
            => descriptor.ServiceType == typeof(IHostedService)
                && (descriptor.ImplementationType == typeof(MediaVolumeMetricsWorker)
                    || descriptor.ImplementationType == typeof(RabbitMqDeadLetterMetricsWorker));

        Assert.DoesNotContain(ServicesFor("api"), IsWorkerGaugeService);
        Assert.Contains(ServicesFor("worker"), IsWorkerGaugeService);
    }

    [Fact(DisplayName = nameof(DeadLetterGaugeReadsDepthFromRabbitManagementApi))]
    [Trait("Layer", "Media outbox metrics - Integration")]
    public async Task DeadLetterGaugeReadsDepthFromRabbitManagementApi()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var rabbitOptions = CreateRabbitOptions(heartbeatQueue: "media.platform-heartbeat");
        var deadLetterQueue = $"{rabbitOptions.HeartbeatQueue}.dlq";
        await using var connection = new RabbitMqConnectionProvider(Options.Create(rabbitOptions));
        await using (var channel = await connection.CreateChannelAsync(cancellationToken))
        {
            await channel.QueueDeclareAsync(
                deadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
                cancellationToken: cancellationToken);
            await channel.QueuePurgeAsync(deadLetterQueue, cancellationToken);
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: deadLetterQueue,
                mandatory: true,
                basicProperties: new BasicProperties(),
                body: "{}"u8.ToArray(),
                cancellationToken: cancellationToken);
        }

        using var metrics = new MetricCapture();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var options = Options.Create(rabbitOptions);
        var managementClient = new RabbitMqManagementClient(httpClient, options);
        using var worker = new RabbitMqDeadLetterMetricsWorker(
            managementClient,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RabbitMqDeadLetterMetricsWorker>.Instance);

        await WaitForDeadLetterDepthAsync(worker, metrics, cancellationToken);
        var measurement = Assert.Single(metrics.Measurements("media.messaging.dlq.messages"), value => value.Value == 1);
        Assert.Equal(deadLetterQueue, measurement.Queue);
    }

    private async Task WaitForDeadLetterDepthAsync(
        RabbitMqDeadLetterMetricsWorker worker,
        MetricCapture metrics,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                await worker.RefreshAsync(cancellationToken);
                metrics.Observe();
                if (metrics.Measurements("media.messaging.dlq.messages").Any(value => value.Value == 1))
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        Assert.Fail("RabbitMQ Management API did not report the message in the dead-letter queue.");
    }

    private ServiceProvider CreateMessagingServices(RabbitMqOptions rabbitOptions, int maxAttempts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(fixture.PostgreSql.GetConnectionString()));
        services.AddSingleton<IOptions<RabbitMqOptions>>(Options.Create(rabbitOptions));
        services.AddSingleton<IOptions<OutboxOptions>>(Options.Create(new OutboxOptions
        {
            PollingIntervalSeconds = 1,
            BatchSize = 1,
            MaxAttempts = maxAttempts,
        }));
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<RabbitMqTopologyInitializer>();
        services.AddSingleton<OutboxPublisherWorker>();
        services.AddSingleton<MediaVolumeMetricsWorker>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private ServiceProvider CreateMetricsServices(int maxAttempts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(fixture.PostgreSql.GetConnectionString()));
        services.AddSingleton<IOptions<OutboxOptions>>(Options.Create(new OutboxOptions { MaxAttempts = maxAttempts }));
        services.AddSingleton<MediaVolumeMetricsWorker>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private RabbitMqOptions CreateRabbitOptions(string? heartbeatQueue = null)
    {
        var suffix = Guid.CreateVersion7().ToString("N");
        return new RabbitMqOptions
        {
            Host = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            Username = "code_for_coders",
            Password = "code_for_coders",
            ManagementUri = $"http://{fixture.RabbitMq.Hostname}:{fixture.RabbitMq.GetMappedPublicPort(15672)}",
            Exchange = $"media.integration.outbox.{suffix}",
            DeadLetterExchange = $"media.integration.outbox.dlx.{suffix}",
            HeartbeatQueue = heartbeatQueue ?? $"media.integration.heartbeat.{suffix}",
            AuditQueue = $"media.integration.outbox.audit.{suffix}",
        };
    }

    private async Task<Guid> SeedOutboxMessageAsync(
        string routingKey,
        DateTimeOffset occurredOn,
        int failedAttempts,
        CancellationToken cancellationToken)
    {
        var messageId = Guid.CreateVersion7();
        var draft = new OutboxMessageDraft(
            messageId,
            Guid.CreateVersion7(),
            routingKey,
            routingKey,
            new { messageId },
            occurredOn,
            null);
        var message = OutboxMessage.Create(draft, "{}");
        for (var attempt = 0; attempt < failedAttempts; attempt++)
        {
            message.RegisterFailure(new InvalidOperationException("Test attempt exhausted."));
        }

        await using var dbContext = CreateDbContext();
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return messageId;
    }

    private async Task ResetOutboxAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE media_access.outbox_messages", cancellationToken);
    }

    private async Task WaitForOutboxStateAsync(
        Guid messageId,
        Func<DateTimeOffset?, int, bool> condition,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var dbContext = CreateDbContext();
            var state = await dbContext.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
                .Where(message => message.Id == messageId)
                .Select(message => new { message.ProcessedOn, message.Attempts })
                .SingleOrDefaultAsync(cancellationToken);
            if (state is not null && condition(state.ProcessedOn, state.Attempts))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        Assert.Fail("Outbox worker did not reach the expected persisted state.");
    }

    private MediaDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<MediaDbContext>().UseNpgsql(fixture.PostgreSql.GetConnectionString()).Options, new TenantContext());

    private sealed class MetricCapture : IDisposable
    {
        private static readonly HashSet<string> InstrumentNames =
        [
            "media.outbox.published",
            "media.outbox.publish_failed",
            "media.outbox.pending",
            "media.outbox.oldest_pending",
            "media.outbox.exhausted",
            "media.messaging.dlq.messages",
        ];

        private readonly ConcurrentQueue<MetricValue> measurements = new();

        public MetricCapture()
        {
            Listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == MediaTelemetry.Meter && InstrumentNames.Contains(instrument.Name))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            Listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                measurements.Enqueue(new MetricValue(instrument.Name, value, tags.ToArray())));
            Listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                measurements.Enqueue(new MetricValue(instrument.Name, value, tags.ToArray())));
            Listener.Start();
        }

        private MeterListener Listener { get; } = new();

        public MetricValue[] Measurements(string name)
            => measurements.Where(measurement => measurement.Name == name).ToArray();

        public void Observe()
            => Listener.RecordObservableInstruments();

        public void Dispose()
            => Listener.Dispose();
    }

    private sealed record MetricValue(string Name, double Value, KeyValuePair<string, object?>[] Tags)
    {
        public string? Event => Tags.SingleOrDefault(tag => tag.Key == "event").Value as string;

        public string? Queue => Tags.SingleOrDefault(tag => tag.Key == "queue").Value as string;
    }
}
