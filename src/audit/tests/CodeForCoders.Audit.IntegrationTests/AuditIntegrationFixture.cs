using CodeForCoders.Audit.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Audit.IntegrationTests;

public sealed class AuditIntegrationFixture : IAsyncLifetime
{
    public const string RuntimeUsername = "code_for_coders_audit_runtime";
    public const string WriterRole = "code_for_coders_audit_writer";
    public const string RuntimePassword = "audit-runtime-test-password";
    public static readonly string[] ProducerRoleNames =
    [
        "code_for_coders_bff_admin",
        "code_for_coders_bff_student",
        "code_for_coders_commerce",
        "code_for_coders_identity",
        "code_for_coders_learning",
        "code_for_coders_media",
        "code_for_coders_notification",
    ];

    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_audit")
        .WithUsername("code_for_coders_audit")
        .WithPassword("code_for_coders_audit")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    public string MigrationConnectionString => PostgreSql.GetConnectionString();

    public string RuntimeConnectionString
    {
        get
        {
            var connectionString = new NpgsqlConnectionStringBuilder(PostgreSql.GetConnectionString())
            {
                Username = RuntimeUsername,
                Password = RuntimePassword,
            };
            return connectionString.ConnectionString;
        }
    }

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync());
        await ProvisionCredentialsAsync();

        var dbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(MigrationConnectionString)
            .Options;
        await using (var dbContext = new AuditDbContext(dbOptions))
        {
            await dbContext.Database.MigrateAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }

    private async Task ProvisionCredentialsAsync()
    {
        var connectionString = PostgreSql.GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE ROLE code_for_coders_audit_writer NOLOGIN;
            CREATE ROLE code_for_coders_audit_runtime LOGIN PASSWORD 'audit-runtime-test-password';
            GRANT code_for_coders_audit_writer TO code_for_coders_audit_runtime;

            CREATE ROLE code_for_coders_bff_admin LOGIN;
            CREATE ROLE code_for_coders_bff_student LOGIN;
            CREATE ROLE code_for_coders_commerce LOGIN;
            CREATE ROLE code_for_coders_identity LOGIN;
            CREATE ROLE code_for_coders_learning LOGIN;
            CREATE ROLE code_for_coders_media LOGIN;
            CREATE ROLE code_for_coders_notification LOGIN;
            """;
        await command.ExecuteNonQueryAsync();

    }
}

[CollectionDefinition(Name)]
public sealed class AuditIntegrationCollection : ICollectionFixture<AuditIntegrationFixture>
{
    public const string Name = "audit-integration";
}
