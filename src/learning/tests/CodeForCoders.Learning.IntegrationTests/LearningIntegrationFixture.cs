using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class LearningIntegrationFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_learning")
        .WithUsername("code_for_coders_learning")
        .WithPassword("code_for_coders_learning")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync());

        var dbOptions = new DbContextOptionsBuilder<LearningDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new LearningDbContext(dbOptions, new TenantContext()))
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
public sealed class LearningIntegrationCollection : ICollectionFixture<LearningIntegrationFixture>
{
    public const string Name = "learning-integration";
}
