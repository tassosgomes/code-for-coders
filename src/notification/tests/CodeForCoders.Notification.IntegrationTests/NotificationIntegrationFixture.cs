using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Notification.IntegrationTests;

public sealed class NotificationIntegrationFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_notification")
        .WithUsername("code_for_coders_notification")
        .WithPassword("code_for_coders_notification")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync());

        var dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new NotificationDbContext(dbOptions, new TenantContext()))
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
            "outbox_messages",
            "delivery_records",
            "delivery_outcome_counters",
        ];
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"DELETE FROM notification_access.{table} WHERE namespace = $1";
            command.Parameters.AddWithValue(processingNamespace);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class NotificationIntegrationCollection : ICollectionFixture<NotificationIntegrationFixture>
{
    public const string Name = "notification-integration";
}
