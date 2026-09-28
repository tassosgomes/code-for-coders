using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class BffAdminIntegrationFixture : IAsyncLifetime
{
    public const string PublisherUsername = "code_for_coders_bff_admin";
    public const string PublisherPassword = "bff-admin-test-password";

    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_bff_admin")
        .WithUsername("code_for_coders_bff_admin")
        .WithPassword("code_for_coders_bff_admin")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
        .WithUsername("code_for_coders")
        .WithPassword("code_for_coders")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(PostgreSql.StartAsync(), RabbitMq.StartAsync());
        await ProvisionPublisherPermissionsAsync();

        var dbOptions = new DbContextOptionsBuilder<BffAdminDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using (var dbContext = new BffAdminDbContext(dbOptions, new TenantContext()))
        {
            await dbContext.Database.MigrateAsync();
        }
    }

    /// <summary>
    /// The collection shares one outbox table and the worker leases any pending row, so a test that
    /// starts a worker first settles rows left pending by other tests to stay independent of order.
    /// </summary>
    public async Task SettlePendingOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        var dbOptions = new DbContextOptionsBuilder<BffAdminDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new BffAdminDbContext(dbOptions, new TenantContext());
        var settledOn = DateTimeOffset.UtcNow;
        await dbContext.OutboxMessages.IgnoreQueryFilters()
            .Where(message => message.ProcessedOn == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.ProcessedOn, settledOn)
                    .SetProperty(message => message.LeaseToken, (Guid?)null)
                    .SetProperty(message => message.LeaseExpiresOn, (DateTimeOffset?)null),
                cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }

    private async Task ProvisionPublisherPermissionsAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var addUser = await RabbitMq.ExecAsync(
            ["rabbitmqctl", "add_user", PublisherUsername, PublisherPassword],
            cancellationToken);
        if (addUser.ExitCode != 0)
        {
            throw new InvalidOperationException("The BFF RabbitMQ test user could not be created.");
        }

        var permissions = await RabbitMq.ExecAsync(
            [
                "rabbitmqctl",
                "set_permissions",
                "-p",
                "/",
                PublisherUsername,
                "^(bff-admin\\.events|bff-admin\\.events\\.dlx|bff-admin\\.platform-heartbeat|bff-admin\\.platform-heartbeat\\.dlq)$",
                "^(bff-admin\\.events|bff-admin\\.platform-heartbeat|bff-admin\\.platform-heartbeat\\.dlq|audit\\.events)$",
                "^(bff-admin\\.events|bff-admin\\.events\\.dlx|bff-admin\\.platform-heartbeat|bff-admin\\.platform-heartbeat\\.dlq)$",
            ],
            cancellationToken);
        if (permissions.ExitCode != 0)
        {
            throw new InvalidOperationException("The BFF RabbitMQ test permissions could not be configured.");
        }
    }
}

[CollectionDefinition(Name)]
public sealed class BffAdminIntegrationCollection : ICollectionFixture<BffAdminIntegrationFixture>
{
    public const string Name = "bff-admin-integration";
}
