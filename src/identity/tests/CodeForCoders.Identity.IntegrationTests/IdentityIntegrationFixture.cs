using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

public sealed class IdentityIntegrationFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_identity")
        .WithUsername("code_for_coders_identity")
        .WithPassword("code_for_coders_identity")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync());

        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new IdentityDbContext(dbOptions, new TenantContext()))
        {
            await dbContext.Database.MigrateAsync();
        }
    }

    public IdentityDbContext CreateDbContext(Guid tenantId)
    {
        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);
        return new IdentityDbContext(dbOptions, tenantContext);
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IdentityIntegrationCollection : ICollectionFixture<IdentityIntegrationFixture>
{
    public const string Name = "identity-integration";
}
