using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using CodeForCoders.Commerce.Infra.Messaging;
using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RabbitMQ.Client;
using Xunit;
namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class EntitlementOutboxPublishingTests(CommerceIntegrationFixture infra)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    [Fact(DisplayName = nameof(WorkerDrainsEntitlementAndDeliversAuditWithCorrelation))]
    public async Task WorkerDrainsEntitlementAndDeliversAuditWithCorrelation()
    {
        var suffix = Guid.CreateVersion7().ToString("N");
        await using var factory = await FactoryAsync(suffix); using var client = factory.CreateClient();
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        var auditQueue = "audit.courtesy-test-" + suffix;
        await channel.QueueDeclareAsync(auditQueue, true, false, false, new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: Cancellation);
        await channel.QueueBindAsync(auditQueue, "audit.courtesy-" + suffix, "auditoria.ato-praticado.v1", null, cancellationToken: Cancellation);
        var tenant = Guid.CreateVersion7(); var id = Guid.CreateVersion7(); var actId = Guid.CreateVersion7();
        const string trace = "00-11111111111111111111111111111111-2222222222222222-01";
        await AppendAsync(factory, tenant, id, "matricula.acesso-concedido.v1", trace);
        await AppendAsync(factory, tenant, actId, "auditoria.ato-praticado.v1", trace);
        await WaitAsync(factory, id, row => row.ProcessedOn != null); await WaitAsync(factory, actId, row => row.ProcessedOn != null);
        Assert.NotNull(await channel.BasicGetAsync("commerce.retention-" + suffix, true, Cancellation));
        var act = await channel.BasicGetAsync(auditQueue, true, Cancellation); Assert.NotNull(act);
        Assert.Equal(trace, System.Text.Encoding.UTF8.GetString((byte[])act.BasicProperties.Headers!["correlationId"]!));
        await channel.QueueDeleteAsync(auditQueue, false, false, Cancellation);
    }
    [Fact(DisplayName = nameof(MissingRetentionExhaustsAttemptsInsteadOfMarkingFactDelivered))]
    public async Task MissingRetentionExhaustsAttemptsInsteadOfMarkingFactDelivered()
    {
        var suffix = Guid.CreateVersion7().ToString("N"); await using var factory = await FactoryAsync(suffix); using var client = factory.CreateClient();
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        await channel.QueueDeleteAsync("commerce.retention-" + suffix, false, false, Cancellation);
        var id = Guid.CreateVersion7(); await AppendAsync(factory, Guid.CreateVersion7(), id, "matricula.acesso-concedido.v1", null);
        var row = await WaitAsync(factory, id, row => row.Attempts == 3); Assert.Null(row.ProcessedOn); Assert.Equal("OutboxPublishException", row.LastError);
    }
    [Fact(DisplayName = nameof(CommerceRuntimeImageResolvesSchoolTimeZoneAndPassesDomainCases))]
    public async Task CommerceRuntimeImageResolvesSchoolTimeZoneAndPassesDomainCases()
    {
        var root = Root();
        await using var image = new ImageFromDockerfileBuilder().WithDockerfileDirectory(root)
            .WithDockerfile("src/commerce/Dockerfile").Build();
        await image.CreateAsync(Cancellation);
        await using var container = new ContainerBuilder(image).WithEntrypoint("/bin/sh").WithCommand("-c", "sleep 300")
            .WithBindMount(Path.Combine(root, "src/commerce/tests/CodeForCoders.Commerce.UnitTests/bin/Debug/net10.0"), "/proof")
            .Build();
        await container.StartAsync(Cancellation);
        var result = await container.ExecAsync(["dotnet", "/proof/CodeForCoders.Commerce.UnitTests.dll", "--filter-class", "CodeForCoders.Commerce.UnitTests.AccessTermTests", "--minimum-expected-tests", "9", "--results-directory", "/tmp/proof-results"], Cancellation);
        Assert.True(result.ExitCode == 0, result.Stdout + result.Stderr);
        Assert.Contains("Test run summary: Passed!", result.Stdout);
    }
    private async Task<CatalogCourseApiFactory> FactoryAsync(string suffix)
    {
        var connection = new NpgsqlConnectionStringBuilder(infra.PostgreSql.GetConnectionString());
        await using (var admin = new NpgsqlConnection(connection.ConnectionString))
        {
            await admin.OpenAsync(Cancellation);
            await using var command = admin.CreateCommand();
            command.CommandText = $"CREATE DATABASE outbox_{suffix}";
            await command.ExecuteNonQueryAsync(Cancellation);
        }
        connection.Database = "outbox_" + suffix;
        await using (var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(connection.ConnectionString).Options, new TenantContext()))
            await db.Database.MigrateAsync(Cancellation);
        return new(infra)
        {
            DatabaseConnectionString = connection.ConnectionString,
            CustomizeServices = services =>
            {
                services.Configure<RabbitMqOptions>(options =>
                {
                    options.Exchange = "commerce.entitlement-" + suffix;
                    options.AuditExchange = "audit.courtesy-" + suffix; options.EntitlementFactRetentionQueue = "commerce.retention-" + suffix;
                    options.OfferRetentionQueue = "commerce.offers-" + suffix; options.HeartbeatQueue = "commerce.heartbeat-" + suffix;
                });
                services.Configure<OutboxOptions>(options => { options.PollingIntervalSeconds = 1; options.MaxAttempts = 3; });
            }
        };
    }
    private static async Task AppendAsync(CatalogCourseApiFactory factory, Guid tenant, Guid id, string key, string? trace)
    {
        await using var scope = factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant);
        await scope.ServiceProvider.GetRequiredService<IEntitlementOutboxMessageWriter>().AppendAsync(new(id, tenant, "CourtesyProof", key, new { eventId = id }, DateTimeOffset.UtcNow, trace), Cancellation);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation);
    }
    private static async Task<EntitlementOutboxMessage> WaitAsync(CatalogCourseApiFactory factory, Guid id, Func<EntitlementOutboxMessage, bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var row = await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().EntitlementOutboxMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync(item => item.Id == id, Cancellation);
            if (predicate(row)) return row; await Task.Delay(100, Cancellation);
        }
        throw new TimeoutException("Entitlement outbox did not reach its expected state.");
    }
    private static string Root()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src/commerce/Dockerfile"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
