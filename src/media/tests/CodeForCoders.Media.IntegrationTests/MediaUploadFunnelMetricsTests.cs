using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(MediaUploadFunnelMetricsCollection.Name)]
public sealed class MediaUploadFunnelMetricsTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(NewUploadIncrementsCreatedCounter))]
    [Trait("Layer", "Media upload funnel metrics - Integration")]
    public async Task NewUploadIncrementsCreatedCounter()
    {
        await ResetUploadsAsync();
        using var metrics = new MetricCapture();
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);

        using var response = await StartUploadAsync(client, token, "created-key", "created-fingerprint-001");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1L, metrics.CounterValue("media.upload.created"));
        Assert.Empty(metrics.Measurements("media.upload.created").Single().Tags);
    }

    [Fact(DisplayName = nameof(ResumedUploadDoesNotIncrementCreatedCounter))]
    [Trait("Layer", "Media upload funnel metrics - Integration")]
    public async Task ResumedUploadDoesNotIncrementCreatedCounter()
    {
        await ResetUploadsAsync();
        using var metrics = new MetricCapture();
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);

        using var created = await StartUploadAsync(client, token, "created-first-key", "resume-fingerprint-001");
        using var resumed = await StartUploadAsync(client, token, "created-resume-key", "resume-fingerprint-001");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal(1L, metrics.CounterValue("media.upload.created"));
        Assert.Single(metrics.Measurements("media.upload.created"));
    }

    [Fact(DisplayName = nameof(CompletedUploadRecordsCounterAndOriginalSize))]
    [Trait("Layer", "Media upload funnel metrics - Integration")]
    public async Task CompletedUploadRecordsCounterAndOriginalSize()
    {
        await ResetUploadsAsync();
        using var metrics = new MetricCapture();
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        const long fileSize = 17;
        using var created = await StartUploadAsync(client, token, "complete-create-key", "complete-fingerprint-001", fileSize);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var uploadId = await ReadUploadIdAsync(created);
        var partUrl = await GetPartUrlAsync(client, token, uploadId);
        using var part = await PutPartAsync(partUrl, fileSize);
        Assert.Equal(HttpStatusCode.OK, part.StatusCode);

        using var completed = await CompleteUploadAsync(client, token, uploadId, "complete-finish-key");

        Assert.Equal(HttpStatusCode.Created, completed.StatusCode);
        Assert.Equal(1L, metrics.CounterValue("media.upload.completed"));
        Assert.Equal(fileSize, Assert.Single(metrics.Measurements("media.upload.size")).Value);
        Assert.Empty(metrics.Measurements("media.upload.completed").Single().Tags);
        Assert.Empty(metrics.Measurements("media.upload.size").Single().Tags);
    }

    [Fact(DisplayName = nameof(ExpiredUploadIncrementsExpiredCounter))]
    [Trait("Layer", "Media upload funnel metrics - Integration")]
    public async Task ExpiredUploadIncrementsExpiredCounter()
    {
        await ResetUploadsAsync();
        var clock = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var clockFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var metrics = new MetricCapture();
        using var client = clockFactory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "expire-create-key", "expire-fingerprint-001");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var uploadId = await ReadUploadIdAsync(created);
        clock.Advance(TimeSpan.FromHours(25));

        await using var scope = clockFactory.Services.CreateAsyncScope();
        var expiration = scope.ServiceProvider.GetRequiredService<IExpirePendingVideoUploads>();
        Assert.True(await expiration.ExpireAsync(uploadId, TestContext.Current.CancellationToken));

        Assert.Equal(1L, metrics.CounterValue("media.upload.expired"));
        Assert.Empty(metrics.Measurements("media.upload.expired").Single().Tags);
    }

    [Fact(DisplayName = nameof(PendingGaugeReportsDatabaseSnapshotWithoutDimensions))]
    [Trait("Layer", "Media upload funnel metrics - Integration")]
    public async Task PendingGaugeReportsDatabaseSnapshotWithoutDimensions()
    {
        await ResetUploadsAsync();
        using var metrics = new MetricCapture();
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var first = await StartUploadAsync(client, token, "pending-first-key", "pending-fingerprint-001");
        using var second = await StartUploadAsync(client, token, "pending-second-key", "pending-fingerprint-002");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        using var worker = new MediaVolumeMetricsWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<TimeProvider>(),
            factory.Services.GetRequiredService<ILogger<MediaVolumeMetricsWorker>>());
        await worker.RefreshAsync(TestContext.Current.CancellationToken);
        metrics.Listener.RecordObservableInstruments();

        var pendingSnapshot = Assert.Single(metrics.Measurements("media.uploads.pending"), measurement => measurement.Value == 2);
        Assert.Empty(pendingSnapshot.Tags);
    }

    private async Task ResetUploadsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE media_access.video_uploads CASCADE",
            TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> StartUploadAsync(
        HttpClient client,
        string token,
        string idempotencyKey,
        string fingerprint,
        long fileSize = 17)
    {
        using var request = AuthorizedRequest(HttpMethod.Post, "/internal/v1/video-uploads", token);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new
        {
            title = "Funnel metrics integration",
            fileName = "funnel-metrics.mp4",
            fileSize,
            contentType = "video/mp4",
            fingerprint,
            uploaderName = "Integration actor",
        });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<Uri> GetPartUrlAsync(HttpClient client, string token, Guid uploadId)
    {
        using var request = AuthorizedRequest(HttpMethod.Post, $"/internal/v1/video-uploads/{uploadId:D}/part-urls", token);
        request.Content = JsonContent.Create(new { partNumbers = new[] { 1 } });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        return new Uri(body.RootElement.GetProperty("parts")[0].GetProperty("url").GetString()!, UriKind.Absolute);
    }

    private static async Task<HttpResponseMessage> PutPartAsync(Uri url, long length)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new ByteArrayContent(new byte[checked((int)length)]),
        };
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> CompleteUploadAsync(
        HttpClient client,
        string token,
        Guid uploadId,
        string idempotencyKey)
    {
        using var request = AuthorizedRequest(HttpMethod.Post, $"/internal/v1/video-uploads/{uploadId:D}/complete", token);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

    private static async Task<Guid> ReadUploadIdAsync(HttpResponseMessage response)
    {
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("uploadId").GetGuid();
    }

    private sealed class MetricCapture : IDisposable
    {
        private readonly ConcurrentQueue<MetricValue> measurements = new();

        public MetricCapture()
        {
            Listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == MediaTelemetry.Meter && instrument.Name.StartsWith("media.upload", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            Listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                measurements.Enqueue(new MetricValue(instrument.Name, value, tags.ToArray())));
            Listener.Start();
        }

        public MeterListener Listener { get; } = new();

        public MetricValue[] Measurements(string name)
            => measurements.Where(measurement => measurement.Name == name).ToArray();

        public long CounterValue(string name)
            => Measurements(name).Sum(measurement => measurement.Value);

        public void Dispose()
            => Listener.Dispose();
    }

    private sealed record MetricValue(string Name, long Value, KeyValuePair<string, object?>[] Tags);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MediaUploadFunnelMetricsCollection : ICollectionFixture<VideoLibraryApiFactory>
{
    public const string Name = "media-upload-funnel-metrics-integration";
}
