using CodeForCoders.Audit.Application.Exceptions;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Application.UseCases.Audit.SearchAuditRecords;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.ValueObjects;
using CodeForCoders.Audit.Infra.Data;
using CodeForCoders.Audit.Infra.Data.Configuration;
using CodeForCoders.Audit.Infra.Data.Health;
using CodeForCoders.Audit.Infra.Data.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace CodeForCoders.Audit.IntegrationTests;

[Collection(AuditIntegrationCollection.Name)]
public sealed class AuditRecordSearchTests(AuditIntegrationFixture fixture)
{
    private static readonly DateTimeOffset FirstDay = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondDay = new(2026, 9, 27, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReceivedOn = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = nameof(AuditRecordSearch_FixesTheFirstPageIdsInValkey))]
    public async Task AuditRecordSearch_FixesTheFirstPageIdsInValkey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken,
            CreateRecord(tenantId, FirstDay, ReceivedOn),
            CreateRecord(tenantId, SecondDay, ReceivedOn.AddSeconds(1)));

        var result = await SearchAsync(scenario, tenantId, sessionId, page: 1, size: 1, cancellationToken);

        Assert.Equal(2, result.Pagination.Total);
        Assert.Equal(2, result.Pagination.TotalPages);
        Assert.Equal(1, result.Pagination.Page);
        Assert.Single(result.Data);
        Assert.StartsWith("snap_", result.Pagination.Snapshot, StringComparison.Ordinal);
        var stored = await scenario.ConnectionProvider.GetAsync(cancellationToken);
        Assert.True(await stored.GetDatabase().KeyExistsAsync(scenario.Options.KeyPrefix + result.Pagination.Snapshot));
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_DoesNotMoveRetroactiveRecordsIntoAnExistingSnapshot))]
    public async Task AuditRecordSearch_DoesNotMoveRetroactiveRecordsIntoAnExistingSnapshot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var first = CreateRecord(tenantId, SecondDay, ReceivedOn.AddSeconds(2));
        var last = CreateRecord(tenantId, FirstDay, ReceivedOn);
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, first, last);
        var pageOne = await SearchAsync(scenario, tenantId, sessionId, 1, 1, cancellationToken);
        var retroactive = CreateRecord(tenantId, FirstDay.AddMinutes(30), ReceivedOn.AddMinutes(1));
        await AddRecordsAsync(scenario, cancellationToken, retroactive);

        var pageTwo = await SearchAsync(
            scenario,
            tenantId,
            sessionId,
            2,
            1,
            cancellationToken,
            snapshot: pageOne.Pagination.Snapshot);

        Assert.Equal(2, pageOne.Pagination.Total);
        Assert.Equal(2, pageTwo.Pagination.Total);
        Assert.Equal(last.Id, pageTwo.Data.Single().Id);
        Assert.DoesNotContain(pageTwo.Data, row => row.Id == retroactive.Id);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_OrdersByPracticedMomentThenRecordId))]
    public async Task AuditRecordSearch_OrdersByPracticedMomentThenRecordId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var sameMoment = FirstDay;
        var records = new[]
        {
            CreateRecord(tenantId, sameMoment, ReceivedOn),
            CreateRecord(tenantId, sameMoment, ReceivedOn.AddSeconds(1)),
            CreateRecord(tenantId, sameMoment.AddMinutes(1), ReceivedOn.AddSeconds(2)),
        };
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, records);

        var result = await SearchAsync(scenario, tenantId, sessionId, 1, 10, cancellationToken);
        var expected = records.OrderByDescending(record => record.PracticedOn).ThenByDescending(record => record.Id).Select(record => record.Id);

        Assert.Equal(expected, result.Data.Select(row => row.Id));
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_IncludesBothDateBounds))]
    public async Task AuditRecordSearch_IncludesBothDateBounds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken,
            CreateRecord(tenantId, FirstDay, ReceivedOn),
            CreateRecord(tenantId, SecondDay, ReceivedOn.AddSeconds(1)),
            CreateRecord(tenantId, FirstDay.AddTicks(-1), ReceivedOn.AddSeconds(2)));

        var result = await SearchAsync(
            scenario,
            tenantId,
            sessionId,
            1,
            10,
            cancellationToken,
            from: FirstDay,
            to: SecondDay);

        Assert.Equal(2, result.Pagination.Total);
        Assert.All(result.Data, row => Assert.InRange(row.PracticedAt!.Value, FirstDay, SecondDay));
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_ExcludesMissingPracticedMomentFromARequestedPeriod))]
    public async Task AuditRecordSearch_ExcludesMissingPracticedMomentFromARequestedPeriod()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var missingMoment = CreateRecord(tenantId, null, ReceivedOn.AddMinutes(2));
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken,
            missingMoment,
            CreateRecord(tenantId, FirstDay, ReceivedOn));

        var result = await SearchAsync(scenario, tenantId, sessionId, 1, 10, cancellationToken, from: FirstDay, to: SecondDay);

        Assert.Single(result.Data);
        Assert.NotEqual(missingMoment.Id, result.Data[0].Id);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_IntersectsTypeAuthorTargetAndComplianceFilters))]
    public async Task AuditRecordSearch_IntersectsTypeAuthorTargetAndComplianceFilters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var authorId = Guid.CreateVersion7();
        var targetId = Guid.CreateVersion7();
        var matching = CreateRecord(tenantId, FirstDay, ReceivedOn, authorId: authorId, targetId: targetId);
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken,
            matching,
            CreateRecord(tenantId, FirstDay, ReceivedOn.AddSeconds(1), authorId: authorId),
            CreateRecord(tenantId, FirstDay, ReceivedOn.AddSeconds(2), targetId: targetId),
            CreateRecord(tenantId, FirstDay, ReceivedOn.AddSeconds(3), type: "papel-alterado", authorId: authorId, targetId: targetId));

        var result = await SearchAsync(
            scenario,
            tenantId,
            sessionId,
            1,
            10,
            cancellationToken,
            type: "papel-concedido",
            authorId: authorId,
            targetId: targetId,
            compliant: true);

        Assert.Equal(matching.Id, result.Data.Single().Id);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_FindsNonConformingRecordsThatRetainTheAuthorReference))]
    public async Task AuditRecordSearch_FindsNonConformingRecordsThatRetainTheAuthorReference()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var authorId = Guid.CreateVersion7();
        var nonConforming = CreateRecord(tenantId, FirstDay, ReceivedOn, type: "papel-alterado", authorId: authorId);
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, nonConforming);

        var result = await SearchAsync(scenario, tenantId, sessionId, 1, 10, cancellationToken, authorId: authorId);

        Assert.Equal(nonConforming.Id, result.Data.Single().Id);
        Assert.False(result.Data[0].Compliant);
        Assert.Equal(authorId, result.Data[0].Author!.Id);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_UsesOnlyTheTenantFromTheSearchInput))]
    public async Task AuditRecordSearch_UsesOnlyTheTenantFromTheSearchInput()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var otherRecord = CreateRecord(otherTenantId, FirstDay, ReceivedOn);
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken,
            CreateRecord(tenantId, FirstDay, ReceivedOn.AddSeconds(1)),
            otherRecord);

        var result = await SearchAsync(scenario, tenantId, sessionId, 1, 10, cancellationToken);

        Assert.Equal(1, result.Pagination.Total);
        Assert.DoesNotContain(result.Data, row => row.Id == otherRecord.Id);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_RejectsSnapshotFromAnotherTenant))]
    public async Task AuditRecordSearch_RejectsSnapshotFromAnotherTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, CreateRecord(tenantId, FirstDay, ReceivedOn));
        var firstPage = await SearchAsync(scenario, tenantId, sessionId, 1, 1, cancellationToken);

        await Assert.ThrowsAsync<AuditFilterInvalidException>(() => SearchAsync(
            scenario,
            otherTenantId,
            sessionId,
            2,
            1,
            cancellationToken,
            snapshot: firstPage.Pagination.Snapshot));
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_RejectsSnapshotFromAnotherStaffSession))]
    public async Task AuditRecordSearch_RejectsSnapshotFromAnotherStaffSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken,
            CreateRecord(tenantId, FirstDay, ReceivedOn),
            CreateRecord(tenantId, SecondDay, ReceivedOn.AddSeconds(1)));
        var firstPage = await SearchAsync(scenario, tenantId, Guid.CreateVersion7(), 1, 1, cancellationToken);

        await Assert.ThrowsAsync<AuditFilterInvalidException>(() => SearchAsync(
            scenario,
            tenantId,
            Guid.CreateVersion7(),
            2,
            1,
            cancellationToken,
            snapshot: firstPage.Pagination.Snapshot));
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_RejectsSnapshotWhenFiltersOrSizeChange))]
    public async Task AuditRecordSearch_RejectsSnapshotWhenFiltersOrSizeChange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken,
            CreateRecord(tenantId, FirstDay, ReceivedOn),
            CreateRecord(tenantId, SecondDay, ReceivedOn.AddSeconds(1)));
        var firstPage = await SearchAsync(scenario, tenantId, sessionId, 1, 1, cancellationToken);

        await Assert.ThrowsAsync<AuditFilterInvalidException>(() => SearchAsync(
            scenario,
            tenantId,
            sessionId,
            2,
            2,
            cancellationToken,
            snapshot: firstPage.Pagination.Snapshot));
        await Assert.ThrowsAsync<AuditFilterInvalidException>(() => SearchAsync(
            scenario,
            tenantId,
            sessionId,
            2,
            1,
            cancellationToken,
            snapshot: firstPage.Pagination.Snapshot,
            type: "papel-revogado"));
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_RejectsMissingExpiredOrInvertedSnapshotInputs))]
    public async Task AuditRecordSearch_RejectsMissingExpiredOrInvertedSnapshotInputs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        await using var scenario = CreateScenario();

        await Assert.ThrowsAsync<AuditFilterInvalidException>(() => SearchAsync(
            scenario,
            tenantId,
            sessionId,
            2,
            10,
            cancellationToken));
        await Assert.ThrowsAsync<AuditFilterInvalidException>(() => SearchAsync(
            scenario,
            tenantId,
            sessionId,
            1,
            10,
            cancellationToken,
            snapshot: "snap_00000000000000000000000000000000000000000"));
        await Assert.ThrowsAsync<AuditFilterInvalidException>(() => SearchAsync(
            scenario,
            tenantId,
            sessionId,
            1,
            10,
            cancellationToken,
            from: SecondDay,
            to: FirstDay));
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_ReturnsAnEmptyPageWithNoDataForAnEmptyTenant))]
    public async Task AuditRecordSearch_ReturnsAnEmptyPageWithNoDataForAnEmptyTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = CreateScenario();

        var result = await SearchAsync(scenario, Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 20, cancellationToken);

        Assert.Empty(result.Data);
        Assert.Equal(0, result.Pagination.Total);
        Assert.Equal(0, result.Pagination.TotalPages);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_ReturnsReferencesWithoutIdentityLabels))]
    public async Task AuditRecordSearch_ReturnsReferencesWithoutIdentityLabels()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var authorId = Guid.CreateVersion7();
        var targetId = Guid.CreateVersion7();
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken,
            CreateRecord(tenantId, FirstDay, ReceivedOn, authorId: authorId, targetId: targetId));

        var result = await SearchAsync(scenario, tenantId, Guid.CreateVersion7(), 1, 20, cancellationToken);

        Assert.Null(result.Data.Single().Author!.Label);
        Assert.Null(result.Data.Single().Target!.Label);
    }

    private Scenario CreateScenario()
    {
        var dbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.RuntimeConnectionString)
            .Options;
        var options = new AuditSnapshotOptions
        {
            ConnectionString = fixture.Valkey.GetConnectionString(),
            KeyPrefix = $"audit-tests:{Guid.CreateVersion7():N}:",
        };
        var provider = new AuditSnapshotConnectionProvider(Options.Create(options));
        var dbContext = new AuditDbContext(dbOptions);
        var search = new SearchAuditRecords(
            new AuditRecordSearchQueries(dbContext),
            new AuditRecordSnapshotStore(provider, Options.Create(options)),
            new SearchAuditRecordsInputValidator());
        return new Scenario(dbContext, provider, search, options);
    }

    private static Task AddRecordsAsync(Scenario scenario, CancellationToken cancellationToken, params AuditRecord[] records)
    {
        scenario.DbContext.AuditRecords.AddRange(records);
        return scenario.DbContext.SaveChangesAsync(cancellationToken);
    }

    private static Task<SearchAuditRecordsOutput> SearchAsync(
        Scenario scenario,
        Guid tenantId,
        Guid sessionId,
        int page,
        int size,
        CancellationToken cancellationToken,
        string? snapshot = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? type = null,
        Guid? authorId = null,
        Guid? targetId = null,
        bool? compliant = null)
        => scenario.Search.ExecuteAsync(
            new SearchAuditRecordsInput(
                tenantId,
                sessionId,
                page,
                size,
                snapshot,
                from,
                to,
                type,
                authorId,
                targetId,
                compliant),
            cancellationToken);

    private static AuditRecord CreateRecord(
        Guid tenantId,
        DateTimeOffset? practicedOn,
        DateTimeOffset receivedOn,
        string type = "papel-concedido",
        Guid? authorId = null,
        Guid? targetId = null)
    {
        var act = new AdministrativeAct(
            Guid.CreateVersion7(),
            "identidade",
            type,
            tenantId,
            practicedOn,
            authorId is null ? new AdministrativeActReference("conta-interna", Guid.CreateVersion7()) : new AdministrativeActReference("conta-interna", authorId),
            targetId is null ? new AdministrativeActReference("conta-interna", Guid.CreateVersion7()) : new AdministrativeActReference("conta-interna", targetId),
            new Dictionary<string, string> { ["papel"] = "professor" },
            "Administrative reason");
        return AuditRecord.Create(act, receivedOn);
    }

    private sealed class Scenario(
        AuditDbContext dbContext,
        AuditSnapshotConnectionProvider connectionProvider,
        SearchAuditRecords search,
        AuditSnapshotOptions options) : IAsyncDisposable
    {
        public AuditDbContext DbContext { get; } = dbContext;

        public AuditSnapshotConnectionProvider ConnectionProvider { get; } = connectionProvider;

        public SearchAuditRecords Search { get; } = search;

        public AuditSnapshotOptions Options { get; } = options;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await ConnectionProvider.DisposeAsync();
        }
    }
}
