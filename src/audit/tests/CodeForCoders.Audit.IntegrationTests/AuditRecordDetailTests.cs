using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Application.UseCases.Audit.GetAuditRecord;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.ValueObjects;
using CodeForCoders.Audit.Infra.Data;
using CodeForCoders.Audit.Infra.Data.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeForCoders.Audit.IntegrationTests;

[Collection(AuditIntegrationCollection.Name)]
public sealed class AuditRecordDetailTests(AuditIntegrationFixture fixture)
{
    private static readonly DateTimeOffset PracticedAt = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReceivedAt = PracticedAt.AddSeconds(4);

    [Fact(DisplayName = nameof(AuditRecordDetail_ReturnsTheOriginalValuesAndNoPhantomComplements))]
    public async Task AuditRecordDetail_ReturnsTheOriginalValuesAndNoPhantomComplements()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var original = CreateOriginal(tenantId);
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, original);

        var result = await scenario.GetAuditRecord.ExecuteAsync(
            new GetAuditRecordInput(tenantId, original.Id),
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(original.Id, result.Id);
        Assert.Equal("identidade", result.Origin);
        Assert.Equal(PracticedAt, result.PracticedAt);
        Assert.Equal(ReceivedAt, result.ReceivedAt);
        Assert.Equal("Administrative reason", result.Reason);
        Assert.Equal("professor", result.Attributes["papel"]);
        Assert.True(result.Compliant);
        Assert.Empty(result.NonComplianceReasons);
        Assert.Empty(result.Complements);
        Assert.False(result.HasComplements);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_PreservesMissingAuthorAndReasonAsReasons))]
    public async Task AuditRecordDetail_PreservesMissingAuthorAndReasonAsReasons()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var original = CreateOriginal(tenantId, includeAuthor: false, reason: null);
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, original);

        var result = await scenario.GetAuditRecord.ExecuteAsync(
            new GetAuditRecordInput(tenantId, original.Id),
            cancellationToken);

        Assert.NotNull(result);
        Assert.False(result.Compliant);
        Assert.Null(result.Author);
        Assert.Null(result.Reason);
        Assert.Contains("autor-ausente", result.NonComplianceReasons);
        Assert.Contains("motivo-ausente", result.NonComplianceReasons);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_ReturnsNullForAnUnknownRecord))]
    public async Task AuditRecordDetail_ReturnsNullForAnUnknownRecord()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = CreateScenario();

        var result = await scenario.GetAuditRecord.ExecuteAsync(
            new GetAuditRecordInput(Guid.CreateVersion7(), Guid.CreateVersion7()),
            cancellationToken);

        Assert.Null(result);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_DoesNotReturnAnotherTenantsRecord))]
    public async Task AuditRecordDetail_DoesNotReturnAnotherTenantsRecord()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var recordTenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var original = CreateOriginal(recordTenantId);
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, original);

        var result = await scenario.GetAuditRecord.ExecuteAsync(
            new GetAuditRecordInput(otherTenantId, original.Id),
            cancellationToken);

        Assert.Null(result);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_DoesNotTreatAComplementAsAnOriginal))]
    public async Task AuditRecordDetail_DoesNotTreatAComplementAsAnOriginal()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var original = CreateOriginal(tenantId);
        var complement = CreateComplement(tenantId, original.Id, PracticedAt.AddMinutes(1));
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, original, complement);

        var result = await scenario.GetAuditRecord.ExecuteAsync(
            new GetAuditRecordInput(tenantId, complement.Id),
            cancellationToken);

        Assert.Null(result);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_OrdersComplementsByConfirmationMomentThenId))]
    public async Task AuditRecordDetail_OrdersComplementsByConfirmationMomentThenId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var original = CreateOriginal(tenantId);
        var sameMoment = PracticedAt.AddMinutes(3);
        var complements = new[]
        {
            CreateComplement(tenantId, original.Id, sameMoment),
            CreateComplement(tenantId, original.Id, sameMoment),
            CreateComplement(tenantId, original.Id, sameMoment.AddMinutes(-1)),
        };
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, [original, .. complements]);

        var result = await scenario.GetAuditRecord.ExecuteAsync(
            new GetAuditRecordInput(tenantId, original.Id),
            cancellationToken);

        Assert.NotNull(result);
        Assert.True(result.HasComplements);
        Assert.Equal(
            complements.OrderBy(record => record.ConfirmedAt).ThenBy(record => record.Id).Select(record => record.Id),
            result.Complements.Select(complement => complement.Id));
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_ReadDoesNotChangeTheOriginalOrAddRows))]
    public async Task AuditRecordDetail_ReadDoesNotChangeTheOriginalOrAddRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var original = CreateOriginal(tenantId);
        await using var scenario = CreateScenario();
        await AddRecordsAsync(scenario, cancellationToken, original);
        var before = await scenario.DbContext.AuditRecords.AsNoTracking()
            .SingleAsync(record => record.Id == original.Id, cancellationToken);

        _ = await scenario.GetAuditRecord.ExecuteAsync(new GetAuditRecordInput(tenantId, original.Id), cancellationToken);
        var persisted = await scenario.DbContext.AuditRecords.AsNoTracking()
            .SingleAsync(record => record.Id == original.Id, cancellationToken);
        var count = await scenario.DbContext.AuditRecords.CountAsync(record => record.TenantId == tenantId, cancellationToken);

        Assert.Equal(1, count);
        Assert.Equal(before.Fingerprint, persisted.Fingerprint);
        Assert.Equal(before.Conformity, persisted.Conformity);
        Assert.Equal(before.Reasons, persisted.Reasons);
        Assert.Equal(before.Complement, persisted.Complement);
        Assert.Null(persisted.OriginalRecordId);
    }

    private Scenario CreateScenario()
    {
        var dbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(fixture.RuntimeConnectionString)
            .Options;
        var dbContext = new AuditDbContext(dbOptions);
        return new Scenario(dbContext, new GetAuditRecord(new AuditRecordDetailQueries(dbContext)));
    }

    private static Task AddRecordsAsync(Scenario scenario, CancellationToken cancellationToken, params AuditRecord[] records)
    {
        scenario.DbContext.AuditRecords.AddRange(records);
        return scenario.DbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuditRecord CreateOriginal(Guid tenantId, bool includeAuthor = true, string? reason = "Administrative reason")
    {
        var act = new AdministrativeAct(
            Guid.CreateVersion7(),
            "identidade",
            "papel-concedido",
            tenantId,
            PracticedAt,
            includeAuthor ? new AdministrativeActReference("conta-interna", Guid.CreateVersion7()) : null,
            new AdministrativeActReference("conta-interna", Guid.CreateVersion7()),
            new Dictionary<string, string> { ["papel"] = "professor" },
            reason);
        return AuditRecord.Create(act, ReceivedAt);
    }

    private static AuditRecord CreateComplement(Guid tenantId, Guid originalRecordId, DateTimeOffset confirmedAt)
        => AuditRecord.CreateComplement(
            tenantId,
            originalRecordId,
            Guid.CreateVersion7(),
            confirmedAt,
            "conta-interna",
            Guid.CreateVersion7(),
            "Follow-up explanation");

    private sealed class Scenario(AuditDbContext dbContext, GetAuditRecord getAuditRecord) : IAsyncDisposable
    {
        public AuditDbContext DbContext { get; } = dbContext;

        public GetAuditRecord GetAuditRecord { get; } = getAuditRecord;

        public ValueTask DisposeAsync() => DbContext.DisposeAsync();
    }
}
