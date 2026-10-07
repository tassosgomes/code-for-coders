using CodeForCoders.Identity.Application;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Contracts;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class AccountFactRetentionTests(IdentityIntegrationFixture fixture)
{
    [Fact]
    public async Task AccountFactsPublishWhenNoConsumerIsBoundYet()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var suffix = Guid.CreateVersion7().ToString("N");
        var exchange = $"identity.facts.{suffix}";
        var retentionQueue = $"identity.account-facts.{suffix}";
        var connectionString = await CreateIsolatedDatabaseAsync(cancellationToken);
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["RabbitMq:Host"] = fixture.RabbitMq.Hostname,
            ["RabbitMq:Port"] = fixture.RabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
            ["RabbitMq:Exchange"] = exchange,
            ["RabbitMq:NotificationExchange"] = $"notification.facts.{suffix}",
            ["RabbitMq:DeadLetterExchange"] = $"identity.facts.{suffix}.dlx",
            ["RabbitMq:HeartbeatQueue"] = $"identity.heartbeat.{suffix}",
            ["RabbitMq:AccountFactRetentionQueue"] = retentionQueue,
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Outbox:BatchSize"] = "10",
            ["Outbox:MaxAttempts"] = "3",
            ["Valkey:ConnectionString"] = "localhost:6379,abortConnect=false",
            ["StudentAccount:ConfirmationBaseUrl"] = "https://students.example.test/confirm-account",
            ["StudentAccount:ConfirmationLifetimeHours"] = "24",
            ["StudentAccount:PasswordResetBaseUrl"] = "https://students.example.test/redefinir-senha",
            ["StudentAccount:PasswordResetLifetimeHours"] = "1",
            ["StaffAccount:PasswordResetBaseUrl"] = "https://staff.example.test/admin/redefinir-senha",
            ["StaffAccount:PasswordResetLifetimeHours"] = "1",
            ["StaffInvitation:AcceptanceBaseUrl"] = "https://staff.example.test/admin/convite",
            ["StaffInvitation:LifetimeHours"] = "168",
            ["Idempotency:FingerprintKeyBase64"] = Convert.ToBase64String(new byte[32]),
            ["OutboxProtection:KeyBase64"] = Convert.ToBase64String(new byte[32]),
        };

        using var host = Host.CreateDefaultBuilder()
            .UseEnvironment("IntegrationTest")
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(configurationValues))
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationConfiguration();
                services.AddDataConfiguration(context.Configuration, context.HostingEnvironment);
                services.AddMessagingConfiguration(context.Configuration);
            })
            .Build();

        await host.StartAsync(cancellationToken);

        var tenantId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        var occurredOn = TimeProvider.System.GetUtcNow();
        var tenant = tenantId.ToString("D");
        var createdId = Guid.CreateVersion7(occurredOn);
        var confirmedId = Guid.CreateVersion7(occurredOn.AddTicks(1));
        var resetId = Guid.CreateVersion7(occurredOn.AddTicks(2));
        OutboxMessageDraft[] facts =
        [
            new(
                createdId,
                tenantId,
                "StudentAccountCreatedV1",
                "identidade.conta-criada.v1",
                new StudentAccountCreatedV1(createdId, tenant, accountId, occurredOn),
                occurredOn,
                TraceParent: null,
                Exchange: exchange,
                CorrelationId: $"identidade-fato-{createdId:D}"),
            new(
                confirmedId,
                tenantId,
                "StudentAccountConfirmedV1",
                "identidade.conta-confirmada.v1",
                new StudentAccountConfirmedV1(confirmedId, tenant, accountId, occurredOn),
                occurredOn,
                TraceParent: null,
                Exchange: exchange,
                CorrelationId: $"identidade-fato-{confirmedId:D}"),
            new(
                resetId,
                tenantId,
                "StudentPasswordResetV1",
                "identidade.senha-redefinida.v1",
                new StudentPasswordResetV1(resetId, tenant, accountId, occurredOn),
                occurredOn,
                TraceParent: null,
                Exchange: exchange,
                CorrelationId: $"identidade-fato-{resetId:D}"),
        ];

        using (var scope = host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
            var writer = scope.ServiceProvider.GetRequiredService<IOutboxMessageWriter>();
            foreach (var fact in facts)
            {
                await writer.AppendAsync(fact, cancellationToken);
            }

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken);
        }

        using (var scope = host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            foreach (var fact in facts)
            {
                await AssertProcessedAsync(dbContext, fact.Id, exchange, cancellationToken);
            }
        }

        await using var channel = await host.Services
            .GetRequiredService<RabbitMqConnectionProvider>()
            .CreateChannelAsync(cancellationToken);
        var received = new List<string>();
        for (var index = 0; index < facts.Length; index++)
        {
            var delivery = await channel.BasicGetAsync(retentionQueue, autoAck: true, cancellationToken);
            Assert.NotNull(delivery);
            Assert.Equal(exchange, delivery.Exchange);
            received.Add(delivery.RoutingKey);
        }

        Assert.Equal(3, received.Count);
        Assert.Contains("identidade.conta-criada.v1", received);
        Assert.Contains("identidade.conta-confirmada.v1", received);
        Assert.Contains("identidade.senha-redefinida.v1", received);

        await host.StopAsync(cancellationToken);
    }

    private async Task<string> CreateIsolatedDatabaseAsync(CancellationToken cancellationToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.PostgreSql.GetConnectionString())
        {
            Database = $"identity_facts_{Guid.CreateVersion7():N}",
        }.ConnectionString;
        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new IdentityDbContext(dbOptions, new TenantContext());
        await dbContext.Database.MigrateAsync(cancellationToken);
        return connectionString;
    }

    private static async Task AssertProcessedAsync(
        IdentityDbContext dbContext,
        Guid eventId,
        string exchange,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var processed = await dbContext.OutboxMessages
                .SingleOrDefaultAsync(message => message.Id == eventId, cancellationToken);
            if (processed?.ProcessedOn is not null)
            {
                Assert.Equal(0, processed.Attempts);
                Assert.Equal(exchange, processed.Exchange);
                return;
            }

            dbContext.ChangeTracker.Clear();
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        Assert.Fail($"Outbox message {eventId} was not processed before the timeout.");
    }
}
