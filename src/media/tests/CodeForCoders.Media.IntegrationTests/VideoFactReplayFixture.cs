using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class VideoFactReplayFixture : IAsyncLifetime
{
    public PostgreSqlContainer Database { get; } = new PostgreSqlBuilder("postgres:18").Build();
    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine").WithUsername("replay-test").WithPassword("replay-test").Build();
    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(Database.StartAsync(), RabbitMq.StartAsync());
        await using var context = new MediaDbContext(new DbContextOptionsBuilder<MediaDbContext>().UseNpgsql(Database.GetConnectionString()).Options, new TenantContext());
        await context.Database.MigrateAsync();
    }
    public async ValueTask DisposeAsync() { await RabbitMq.DisposeAsync(); await Database.DisposeAsync(); }
}

[CollectionDefinition(Name)]
public sealed class VideoFactReplayCollection : ICollectionFixture<VideoFactReplayFixture>
{
    public const string Name = "video-fact-replay";
}
