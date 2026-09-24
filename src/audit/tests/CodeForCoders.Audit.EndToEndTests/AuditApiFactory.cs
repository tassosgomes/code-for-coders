using CodeForCoders.Audit.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Audit.EndToEndTests;

public sealed class AuditApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string RuntimeUsername = "code_for_coders_audit_runtime";
    private const string RuntimePassword = "audit-runtime-test-password";

    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_audit")
        .WithUsername("code_for_coders_audit")
        .WithPassword("code_for_coders_audit")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.StartAsync();
        await ProvisionRuntimeCredentialsAsync();

        var dbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new AuditDbContext(dbOptions);
        await dbContext.Database.MigrateAsync();
    }

    private async Task ProvisionRuntimeCredentialsAsync()
    {
        await using var connection = new NpgsqlConnection(PostgreSql.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE ROLE code_for_coders_audit_writer NOLOGIN;
            CREATE ROLE code_for_coders_audit_runtime LOGIN PASSWORD 'audit-runtime-test-password';
            GRANT code_for_coders_audit_writer TO code_for_coders_audit_runtime;
            """;
        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("EndToEndTest");
        var runtimeConnection = new NpgsqlConnectionStringBuilder(PostgreSql.GetConnectionString())
        {
            Username = RuntimeUsername,
            Password = RuntimePassword,
        };
        builder.UseSetting("ConnectionStrings:DefaultConnection", runtimeConnection.ConnectionString);
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.ConfigureTestServices(services =>
        {
            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var hostedService in hostedServices)
            {
                services.Remove(hostedService);
            }
        });
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AuditApiCollection : ICollectionFixture<AuditApiFactory>
{
    public const string Name = "audit-e2e";
}
