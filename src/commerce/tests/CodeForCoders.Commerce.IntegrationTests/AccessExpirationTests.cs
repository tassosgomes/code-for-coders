using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using CodeForCoders.Commerce.Infra.Data.Entitlement;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
[Trait("Integration", "Access expiration - PostgreSQL and RabbitMQ")]
public sealed class AccessExpirationTests(CommerceIntegrationFixture infra)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(CrossingExclusiveEndProducesOneContractFactAndAtomicMarker))]
    public async Task CrossingExclusiveEndProducesOneContractFactAndAtomicMarker()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var grant = await test.SeedAsync(test.Clock.Now);
        test.Clock.Now = grant.ExpiresAt!.Value.AddTicks(-1);
        Assert.Equal(0, await test.CycleAsync());
        Assert.Equal("allowed", (await test.DecideAsync()).Decision);
        test.Clock.Now = grant.ExpiresAt.Value;
        Assert.Equal(1, await test.CycleAsync());
        var fact = Assert.Single(await test.FactsAsync());
        using var json = JsonDocument.Parse(fact.Payload);
        CommerceMessages.AssertSends("matricula.acesso-expirado.v1", json.RootElement);
        Assert.Equal("matricula.acesso-expirado.v1", fact.RoutingKey);
        Assert.Equal("AcessoExpirado", fact.Type);
        Assert.Equal(test.Tenant, fact.TenantId);
        Assert.Equal(grant.Id, json.RootElement.GetProperty("grantId").GetGuid());
        Assert.Equal(test.Student, json.RootElement.GetProperty("studentId").GetGuid());
        Assert.Equal(test.Course, json.RootElement.GetProperty("courseId").GetGuid());
        Assert.Equal(test.Clock.Now, json.RootElement.GetProperty("occurredAt").GetDateTimeOffset());
        Assert.Equal(grant.ExpiresAt, json.RootElement.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(test.Clock.Now, fact.OccurredOn);
        Assert.StartsWith("access-expiration-", fact.TraceParent);
        var saved = await test.ReloadAsync(grant.Id);
        Assert.Equal(fact.Id, saved.ExpiryEventId);
        Assert.Equal(test.Clock.Now, saved.ExpiryPublishedAt);
        Assert.Equal("active", saved.Status);
    }

    [Fact(DisplayName = nameof(ReexecutingCycleDoesNotRepublishOrChangeMarker))]
    public async Task ReexecutingCycleDoesNotRepublishOrChangeMarker()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var grant = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        Assert.Equal(1, await test.CycleAsync());
        var first = await test.ReloadAsync(grant.Id);
        test.Clock.Now = test.Clock.Now.AddYears(1);
        Assert.Equal(0, await test.CycleAsync());
        Assert.Single(await test.FactsAsync());
        var second = await test.ReloadAsync(grant.Id);
        Assert.Equal(first.ExpiryEventId, second.ExpiryEventId);
        Assert.Equal(first.ExpiryPublishedAt, second.ExpiryPublishedAt);
    }

    [Fact(DisplayName = nameof(ConcurrentInstancesProduceOneFactPerGrantAcrossTenants))]
    public async Task ConcurrentInstancesProduceOneFactPerGrantAcrossTenants()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var first = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        var second = await test.SeedAsync(test.Clock.Now.AddMonths(-2), tenant: Guid.CreateVersion7());
        var results = await Task.WhenAll(test.CycleAsync(), test.CycleAsync());
        Assert.Equal(2, results.Sum());
        var facts = await test.FactsAsync();
        Assert.Equal(2, facts.Length);
        Assert.Equal(2, facts.Select(item => item.Id).Distinct().Count());
        var savedFirst = await test.ReloadAsync(first.Id);
        Assert.Equal(first.TenantId, facts.Single(item => item.Id == savedFirst.ExpiryEventId).TenantId);
        Assert.Contains(facts, item => item.TenantId == second.TenantId);
        Assert.Equal(0, await test.CycleAsync());
    }

    [Fact(DisplayName = nameof(LockedGrantIsSkippedWhileAnotherGrantIsProcessed))]
    public async Task LockedGrantIsSkippedWhileAnotherGrantIsProcessed()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var locked = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        var other = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        await using var scope = test.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(Cancellation);
        await db.AccessGrants.FromSql($"SELECT * FROM entitlement.access_grants WHERE id = {locked.Id} FOR UPDATE")
            .IgnoreQueryFilters().ToListAsync(Cancellation);
        Assert.Equal(1, await test.CycleAsync().WaitAsync(TimeSpan.FromSeconds(10), Cancellation));
        Assert.Null((await test.ReloadAsync(locked.Id)).ExpiryEventId);
        Assert.NotNull((await test.ReloadAsync(other.Id)).ExpiryEventId);
        await transaction.RollbackAsync(Cancellation);
        Assert.Equal(1, await test.CycleAsync());
        Assert.Equal(2, (await test.FactsAsync()).Length);
    }

    [Fact(DisplayName = nameof(LifetimeAndFutureGrantsNeverProduceExpirationFacts))]
    public async Task LifetimeAndFutureGrantsNeverProduceExpirationFacts()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var lifetime = await test.SeedAsync(test.Clock.Now.AddYears(-20), "lifetime");
        var future = await test.SeedAsync(test.Clock.Now);
        Assert.Equal(0, await test.CycleAsync());
        Assert.Empty(await test.FactsAsync());
        Assert.Null((await test.ReloadAsync(future.Id)).ExpiryPublishedAt);
        test.Clock.Now = test.Clock.Now.AddYears(100);
        Assert.Equal(1, await test.CycleAsync());
        Assert.Null((await test.ReloadAsync(lifetime.Id)).ExpiryEventId);
        Assert.Null((await test.ReloadAsync(lifetime.Id)).ExpiryPublishedAt);
        Assert.Single(await test.FactsAsync());
        Assert.Equal("allowed", (await test.DecideAsync()).Decision);
    }

    [Fact(DisplayName = nameof(DisabledWorkerAndLostFactDoNotChangeEndedDecision))]
    public async Task DisabledWorkerAndLostFactDoNotChangeEndedDecision()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var grant = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        var withoutWorker = await test.DecideAsync();
        Assert.Equal("denied", withoutWorker.Decision);
        Assert.Equal("grant-ended", withoutWorker.DeniedReason);
        Assert.Null((await test.ReloadAsync(grant.Id)).ExpiryEventId);
        Assert.Equal(1, await test.CycleAsync());
        Assert.Equal(withoutWorker, await test.DecideAsync());
        await using var scope = test.Scope();
        await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().EntitlementOutboxMessages.ExecuteDeleteAsync(Cancellation);
        Assert.Equal(withoutWorker, await test.DecideAsync());
        Assert.Equal(0, await test.CycleAsync());
    }

    [Fact(DisplayName = nameof(ExpiredGrantFactDoesNotDenyAnotherActiveGrant))]
    public async Task ExpiredGrantFactDoesNotDenyAnotherActiveGrant()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var expired = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        var active = await test.SeedAsync(test.Clock.Now);
        var before = await test.DecideAsync();
        Assert.Equal("allowed", before.Decision);
        Assert.Equal(1, await test.CycleAsync());
        Assert.Equal(before, await test.DecideAsync());
        Assert.NotNull((await test.ReloadAsync(expired.Id)).ExpiryEventId);
        Assert.Null((await test.ReloadAsync(active.Id)).ExpiryEventId);
        Assert.Single(await test.FactsAsync());
    }

    [Fact(DisplayName = nameof(LagAlertFiresOnlyAboveThirtyMinutesAndMetricHasNoPersonTags))]
    public async Task LagAlertFiresOnlyAboveThirtyMinutesAndMetricHasNoPersonTags()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var first = await test.SeedAsync(test.Clock.Now);
        test.Clock.Now = first.ExpiresAt!.Value.AddMinutes(30);
        test.Logs.Clear();
        Assert.Equal(1, await test.CycleAsync());
        Assert.Contains(test.Measurements, item => item.Seconds == 1800 && item.Tags == 0);
        Assert.DoesNotContain(test.Logs, item => item.Contains("Oldest pending lag", StringComparison.Ordinal));
        var second = await test.SeedAsync(test.Clock.Now);
        test.Clock.Now = second.ExpiresAt!.Value.AddMinutes(30).AddSeconds(1);
        Assert.Equal(1, await test.CycleAsync());
        Assert.Contains(test.Measurements, item => item.Seconds == 1801 && item.Tags == 0);
        Assert.Contains(test.Logs, item => item.Contains("Oldest pending lag is 1801", StringComparison.Ordinal));
        Assert.Equal(0, await test.CycleAsync());
        Assert.Equal((0d, 0), test.Measurements.Last());
        Assert.All(test.Measurements, item => Assert.Equal(0, item.Tags));
        var logs = string.Join(" ", test.Logs);
        Assert.DoesNotContain(test.Student.ToString("D"), logs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = nameof(PaidOrdersWithoutAccessForMoreThanTenMinutesAreCountedWithoutIdentifiers))]
    public async Task PaidOrdersWithoutAccessForMoreThanTenMinutesAreCountedWithoutIdentifiers()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var now = test.Clock.Now;
        await test.SeedOrderAsync(null, number: 1);
        await test.SeedOrderAsync(now.AddMinutes(-9), number: 2);
        await test.SeedOrderAsync(now.AddMinutes(-30), granted: true, number: 3);
        await test.CycleAsync();
        Assert.Equal((0L, 0), test.OverdueMeasurements.Last());
        await test.SeedOrderAsync(now.AddMinutes(-11), number: 4);
        await test.SeedOrderAsync(now.AddHours(-3), number: 5);
        await test.CycleAsync();
        Assert.Equal((2L, 0), test.OverdueMeasurements.Last());
        Assert.All(test.OverdueMeasurements, item => Assert.Equal(0, item.Tags));
    }

    [Fact(DisplayName = nameof(FailedBatchRollsBackAllFactsAndMarkersAndCanBeRetried))]
    public async Task FailedBatchRollsBackAllFactsAndMarkersAndCanBeRetried()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra);
        var first = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        var second = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        await using (var scope = test.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var cycle = new AccessExpirationCycle(db, new AccessExpirationFailingWriter(db), test.Clock,
                Microsoft.Extensions.Options.Options.Create(new AccessExpirationOptions()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AccessExpirationCycle>.Instance);
            await Assert.ThrowsAsync<DbUpdateException>(() => cycle.RunAsync(Cancellation));
        }
        Assert.Empty(await test.FactsAsync());
        Assert.Null((await test.ReloadAsync(first.Id)).ExpiryEventId);
        Assert.Null((await test.ReloadAsync(second.Id)).ExpiryPublishedAt);
        Assert.Equal(2, await test.CycleAsync());
        Assert.Equal(2, (await test.FactsAsync()).Length);
    }

    [Fact(DisplayName = nameof(ConfiguredBatchBoundsEachCycleAndLeavesOldestFirst))]
    public async Task ConfiguredBatchBoundsEachCycleAndLeavesOldestFirst()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra,
            customize: services => services.Configure<AccessExpirationOptions>(options => options.BatchSize = 1));
        var newer = await test.SeedAsync(test.Clock.Now.AddMonths(-2));
        var older = await test.SeedAsync(test.Clock.Now.AddMonths(-3));
        Assert.Equal(1, await test.CycleAsync());
        Assert.NotNull((await test.ReloadAsync(older.Id)).ExpiryEventId);
        Assert.Null((await test.ReloadAsync(newer.Id)).ExpiryEventId);
        Assert.Equal(1, await test.CycleAsync());
        Assert.Equal(0, await test.CycleAsync());
    }

    [Fact(DisplayName = nameof(HostedWorkerAndExistingPublisherDeliverExpiredMonthToRetention))]
    public async Task HostedWorkerAndExistingPublisherDeliverExpiredMonthToRetention()
    {
        await using var test = await AccessExpirationFixture.CreateAsync(infra, delivery: true);
        var grant = await test.SeedAsync(test.Clock.Now);
        test.Clock.Now = grant.ExpiresAt!.Value;
        await using var channel = await test.Factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        RabbitMQ.Client.BasicGetResult? delivered = null;
        for (var attempt = 0; attempt < 100 && delivered is null; attempt++)
        {
            delivered = await channel.BasicGetAsync(test.Queue, true, Cancellation);
            if (delivered is null) await Task.Delay(100, Cancellation);
        }
        Assert.NotNull(delivered);
        Assert.Equal("matricula.acesso-expirado.v1", delivered.RoutingKey);
        using var json = JsonDocument.Parse(delivered.Body);
        CommerceMessages.AssertSends("matricula.acesso-expirado.v1", json.RootElement);
        Assert.Equal(grant.Id, json.RootElement.GetProperty("grantId").GetGuid());
        Assert.Equal((await test.ReloadAsync(grant.Id)).ExpiryEventId!.Value.ToString(), delivered.BasicProperties.MessageId);
        Assert.NotNull(delivered.BasicProperties.Headers!["correlationId"]);
        Assert.Single(await test.FactsAsync());
    }
}
