using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CommerceIntegrationFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_commerce")
        .WithUsername("code_for_coders_commerce")
        .WithPassword("code_for_coders_commerce")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    private const int ValkeyPort = 6379;

    public IContainer Valkey { get; } = new ContainerBuilder("valkey/valkey:8.1-alpine")
        .WithPortBinding(ValkeyPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
        .Build();

    public string ValkeyConnectionString
        => $"{Valkey.Hostname}:{Valkey.GetMappedPublicPort(ValkeyPort)},abortConnect=false";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync(), Valkey.StartAsync());

        var dbOptions = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new CommerceDbContext(dbOptions, new TenantContext()))
        {
            await dbContext.Database.MigrateAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Valkey.DisposeAsync();
        await RabbitMq.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class CommerceIntegrationCollection : ICollectionFixture<CommerceIntegrationFixture>
{
    public const string Name = "commerce-integration";
}
