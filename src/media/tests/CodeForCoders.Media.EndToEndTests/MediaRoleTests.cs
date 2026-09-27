using System.Net;
using System.Net.Http.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Media.EndToEndTests;

public sealed class MediaRoleTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_media")
        .WithUsername("code_for_coders_media")
        .WithPassword("code_for_coders_media")
        .Build();

    private readonly RabbitMqContainer rabbitMq = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    private MediaRoleFactory apiFactory = null!;
    private MediaRoleFactory workerFactory = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(postgreSql.StartAsync(), rabbitMq.StartAsync());

        var dbOptions = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql(postgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new MediaDbContext(dbOptions, new TenantContext()))
        {
            await dbContext.Database.MigrateAsync();
        }

        apiFactory = new MediaRoleFactory(
            "api",
            postgreSql.GetConnectionString(),
            rabbitMq.Hostname,
            rabbitMq.GetMappedPublicPort(5672));
        workerFactory = new MediaRoleFactory(
            "worker",
            postgreSql.GetConnectionString(),
            rabbitMq.Hostname,
            rabbitMq.GetMappedPublicPort(5672));
    }

    public async ValueTask DisposeAsync()
    {
        apiFactory.Dispose();
        workerFactory.Dispose();
        await rabbitMq.DisposeAsync();
        await postgreSql.DisposeAsync();
    }

    [Fact]
    public async Task ApiRole_ServesVideoRoutesAndHealth()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = apiFactory.CreateClient();

        using var health = await client.GetAsync("/health/live", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using var videos = await client.GetAsync("/internal/v1/videos", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, videos.StatusCode);
    }

    [Fact]
    public void ApiRole_RegistersAllBackgroundServices()
    {
        var hostedServices = apiFactory.Services.GetServices<IHostedService>().ToList();

        Assert.Contains(hostedServices, service => service is RabbitMqTopologyInitializer);
        Assert.Contains(hostedServices, service => service is OutboxPublisherWorker);
        Assert.Contains(hostedServices, service => service is HeartbeatConsumerWorker);
    }

    [Fact]
    public async Task WorkerRole_ServesOnlyHealth()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = workerFactory.CreateClient();

        using var health = await client.GetAsync("/health/live", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using var videos = await client.GetAsync("/internal/v1/videos", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, videos.StatusCode);

        using var heartbeat = await client.PostAsync(
            "/internal/platform/heartbeat",
            JsonContent.Create(new { }),
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, heartbeat.StatusCode);
    }

    [Fact]
    public void WorkerRole_RegistersPreparationAndExpirationWorkers()
    {
        var hostedServices = workerFactory.Services.GetServices<IHostedService>().ToList();

        Assert.Contains(hostedServices, service => service is RabbitMqTopologyInitializer);
        Assert.Contains(hostedServices, service => service is OutboxPublisherWorker);
        Assert.Contains(hostedServices, service => service is ExpiredVideoUploadWorker);
        Assert.Contains(hostedServices, service => service is VideoPreparationWorker);
        Assert.DoesNotContain(hostedServices, service => service is HeartbeatConsumerWorker);
    }

    [Fact]
    public void InvalidRole_FailsStartupWithClearMessage()
    {
        using var factory = new MediaRoleFactory(
            "scheduler",
            postgreSql.GetConnectionString(),
            rabbitMq.Hostname,
            rabbitMq.GetMappedPublicPort(5672));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("'api'", exception.Message);
        Assert.Contains("'worker'", exception.Message);
    }

    [Fact]
    public void MissingRole_FailsStartupWithClearMessage()
    {
        using var factory = new MediaRoleFactory(
            "api",
            postgreSql.GetConnectionString(),
            rabbitMq.Hostname,
            rabbitMq.GetMappedPublicPort(5672),
            omitRole: true);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("'api'", exception.Message);
        Assert.Contains("'worker'", exception.Message);
    }
}
