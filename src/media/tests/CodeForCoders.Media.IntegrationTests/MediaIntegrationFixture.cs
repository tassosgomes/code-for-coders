using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class MediaIntegrationFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_media")
        .WithUsername("code_for_coders_media")
        .WithPassword("code_for_coders_media")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync());

        var dbOptions = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new MediaDbContext(dbOptions, new TenantContext()))
        {
            await dbContext.Database.MigrateAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MediaIntegrationCollection : ICollectionFixture<MediaIntegrationFixture>
{
    public const string Name = "media-integration";
}
