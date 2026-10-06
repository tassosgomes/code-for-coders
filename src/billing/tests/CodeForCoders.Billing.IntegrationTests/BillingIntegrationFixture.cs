using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Infra.Data;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Billing.IntegrationTests;

public sealed class BillingIntegrationFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_billing")
        .WithUsername("code_for_coders_billing")
        .WithPassword("code_for_coders_billing")
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

        var dbOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new BillingDbContext(dbOptions, new TenantContext()))
        {
            await dbContext.Database.MigrateAsync();
        }
    }

    public async Task PurgeNamespaceAsync(string processingNamespace)
    {
        await using var connection = new NpgsqlConnection(PostgreSql.GetConnectionString());
        await connection.OpenAsync();
        string[] tables =
        [
            "payments",
            "gateway_inbox",
            "outbox_messages",
        ];
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"DELETE FROM billing_access.{table} WHERE namespace = $1";
            command.Parameters.AddWithValue(processingNamespace);
            await command.ExecuteNonQueryAsync();
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
public sealed class BillingIntegrationCollection : ICollectionFixture<BillingIntegrationFixture>
{
    public const string Name = "billing-integration";
}
