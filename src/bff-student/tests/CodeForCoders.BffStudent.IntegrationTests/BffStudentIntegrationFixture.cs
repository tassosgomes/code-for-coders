using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Infra.Data;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.BffStudent.IntegrationTests;

public sealed class BffStudentIntegrationFixture : IAsyncLifetime
{
    private const int ValkeyPort = 6379;

    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_bff_student")
        .WithUsername("code_for_coders_bff_student")
        .WithPassword("code_for_coders_bff_student")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    public IContainer Valkey { get; } = new ContainerBuilder("valkey/valkey:8.1-alpine")
        .WithPortBinding(ValkeyPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
        .Build();

    public string ValkeyConnectionString
        => $"{Valkey.Hostname}:{Valkey.GetMappedPublicPort(ValkeyPort)},abortConnect=false";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync(), Valkey.StartAsync());

        var dbOptions = new DbContextOptionsBuilder<BffStudentDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new BffStudentDbContext(dbOptions, new TenantContext()))
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
public sealed class BffStudentIntegrationCollection : ICollectionFixture<BffStudentIntegrationFixture>
{
    public const string Name = "bff-student-integration";
}
