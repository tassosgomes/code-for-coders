using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Application.UseCases.Audit.ConfirmAuditRecordComplement;
using CodeForCoders.BffAdmin.Infra.Data;
using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using CodeForCoders.BffAdmin.Infra.Data.Idempotency;
using CodeForCoders.BffAdmin.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

[Collection(BffAdminIntegrationCollection.Name)]
public sealed class AuditComplementConfirmationTests(BffAdminIntegrationFixture fixture)
{
    private const string Explanation = "The access change was verified against the internal support case.";
    private const string KeyVersion = "test-key-v1";
    private static readonly string KeyBase64 = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

    [Fact(DisplayName = nameof(AuditComplementConfirmation_StoresCiphertextAndAnAuthenticatedFingerprint))]
    public async Task AuditComplementConfirmation_StoresCiphertextAndAnAuthenticatedFingerprint()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var dbContext = CreateDbContext(tenantId);
        var protector = CreateProtector();
        var store = CreateStore(dbContext, protector);

        var result = await store.ConfirmAsync(draft, TestContext.Current.CancellationToken);

        var message = await dbContext.OutboxMessages.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == draft.ConfirmationId, TestContext.Current.CancellationToken);
        var record = await dbContext.AuditComplementIdempotencyRecords.IgnoreQueryFilters()
            .SingleAsync(item => item.IdempotencyKey == draft.IdempotencyKey, TestContext.Current.CancellationToken);
        Assert.Equal(AuditComplementConfirmationWriteStatus.Accepted, result.Status);
        Assert.Equal(draft.ConfirmationId, result.ConfirmationId);
        Assert.Equal("audit.events", message.DestinationExchange);
        Assert.Equal("auditoria.registro.complemento-confirmado.v1", message.RoutingKey);
        Assert.Equal(KeyVersion, message.PayloadKeyVersion);
        Assert.DoesNotContain(Explanation, message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(Explanation, JsonSerializer.Serialize(record), StringComparison.Ordinal);
        Assert.Equal(32, record.RequestHash.Length);
        using var document = JsonDocument.Parse(protector.Unprotect(message));
        Assert.Equal(Explanation, document.RootElement.GetProperty("explanation").GetString());

        message.RegisterFailure("OUTBOX_PUBLISH_FAILED");
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal("OUTBOX_PUBLISH_FAILED", message.LastError);
        Assert.DoesNotContain(Explanation, message.LastError, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_ReusesTheConfirmationAndOutboxOnAnIdenticalRetry))]
    public async Task AuditComplementConfirmation_ReusesTheConfirmationAndOutboxOnAnIdenticalRetry()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var dbContext = CreateDbContext(tenantId);
        var store = CreateStore(dbContext, CreateProtector());

        var first = await store.ConfirmAsync(draft, TestContext.Current.CancellationToken);
        var retry = await store.ConfirmAsync(draft with
        {
            ConfirmationId = Guid.CreateVersion7(),
            ConfirmedAt = draft.ConfirmedAt.AddMinutes(1),
        }, TestContext.Current.CancellationToken);

        Assert.Equal(AuditComplementConfirmationWriteStatus.Accepted, first.Status);
        Assert.Equal(AuditComplementConfirmationWriteStatus.Replay, retry.Status);
        Assert.Equal(first.ConfirmationId, retry.ConfirmationId);
        Assert.Equal(1, await dbContext.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
        Assert.Equal(1, await dbContext.AuditComplementIdempotencyRecords.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_RejectsTheSameKeyWithDifferentText))]
    public async Task AuditComplementConfirmation_RejectsTheSameKeyWithDifferentText()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var dbContext = CreateDbContext(tenantId);
        var store = CreateStore(dbContext, CreateProtector());

        await store.ConfirmAsync(draft, TestContext.Current.CancellationToken);
        var conflict = await store.ConfirmAsync(draft with
        {
            ConfirmationId = Guid.CreateVersion7(),
            Explanation = "A different explanation.",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(AuditComplementConfirmationWriteStatus.Conflict, conflict.Status);
        Assert.Equal(1, await dbContext.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_RejectsTheSameKeyForADifferentOriginal))]
    public async Task AuditComplementConfirmation_RejectsTheSameKeyForADifferentOriginal()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var dbContext = CreateDbContext(tenantId);
        var store = CreateStore(dbContext, CreateProtector());

        await store.ConfirmAsync(draft, TestContext.Current.CancellationToken);
        var conflict = await store.ConfirmAsync(draft with
        {
            RecordId = Guid.CreateVersion7(),
            ConfirmationId = Guid.CreateVersion7(),
        }, TestContext.Current.CancellationToken);

        Assert.Equal(AuditComplementConfirmationWriteStatus.Conflict, conflict.Status);
        Assert.Equal(1, await dbContext.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_ScopesKeysToTheActor))]
    public async Task AuditComplementConfirmation_ScopesKeysToTheActor()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var dbContext = CreateDbContext(tenantId);
        var store = CreateStore(dbContext, CreateProtector());

        var first = await store.ConfirmAsync(draft, TestContext.Current.CancellationToken);
        var second = await store.ConfirmAsync(draft with
        {
            ActorId = Guid.CreateVersion7(),
            ConfirmationId = Guid.CreateVersion7(),
        }, TestContext.Current.CancellationToken);

        Assert.Equal(AuditComplementConfirmationWriteStatus.Accepted, first.Status);
        Assert.Equal(AuditComplementConfirmationWriteStatus.Accepted, second.Status);
        Assert.NotEqual(first.ConfirmationId, second.ConfirmationId);
        Assert.Equal(2, await dbContext.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_ScopesKeysToTheTenant))]
    public async Task AuditComplementConfirmation_ScopesKeysToTheTenant()
    {
        var firstTenantId = Guid.CreateVersion7();
        var secondTenantId = Guid.CreateVersion7();
        var draft = CreateDraft(firstTenantId);
        await using var firstContext = CreateDbContext(firstTenantId);
        await using var secondContext = CreateDbContext(secondTenantId);
        var firstStore = CreateStore(firstContext, CreateProtector());
        var secondStore = CreateStore(secondContext, CreateProtector());

        var first = await firstStore.ConfirmAsync(draft, TestContext.Current.CancellationToken);
        var second = await secondStore.ConfirmAsync(draft with
        {
            TenantId = secondTenantId,
            ConfirmationId = Guid.CreateVersion7(),
        }, TestContext.Current.CancellationToken);

        Assert.Equal(AuditComplementConfirmationWriteStatus.Accepted, first.Status);
        Assert.Equal(AuditComplementConfirmationWriteStatus.Accepted, second.Status);
        Assert.Equal(1, await firstContext.AuditComplementIdempotencyRecords.IgnoreQueryFilters()
            .CountAsync(record => record.TenantId == firstTenantId, TestContext.Current.CancellationToken));
        Assert.Equal(1, await firstContext.AuditComplementIdempotencyRecords.IgnoreQueryFilters()
            .CountAsync(record => record.TenantId == secondTenantId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_ConcurrentIdenticalRequestsCreateOneOutboxMessage))]
    public async Task AuditComplementConfirmation_ConcurrentIdenticalRequestsCreateOneOutboxMessage()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var firstContext = CreateDbContext(tenantId);
        await using var secondContext = CreateDbContext(tenantId);
        var firstStore = CreateStore(firstContext, CreateProtector());
        var secondStore = CreateStore(secondContext, CreateProtector());

        var results = await Task.WhenAll(
            firstStore.ConfirmAsync(draft, TestContext.Current.CancellationToken),
            secondStore.ConfirmAsync(draft with { ConfirmationId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken));

        Assert.True(results.All(result => result.Status is AuditComplementConfirmationWriteStatus.Accepted
            or AuditComplementConfirmationWriteStatus.Replay));
        Assert.Single(results.Select(result => result.ConfirmationId).Distinct());
        await using var verifyContext = CreateDbContext(tenantId);
        Assert.Equal(1, await verifyContext.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
        Assert.Equal(1, await verifyContext.AuditComplementIdempotencyRecords.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_ConcurrentDifferentRequestsHaveOneWinner))]
    public async Task AuditComplementConfirmation_ConcurrentDifferentRequestsHaveOneWinner()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var firstContext = CreateDbContext(tenantId);
        await using var secondContext = CreateDbContext(tenantId);
        var firstStore = CreateStore(firstContext, CreateProtector());
        var secondStore = CreateStore(secondContext, CreateProtector());

        var results = await Task.WhenAll(
            firstStore.ConfirmAsync(draft, TestContext.Current.CancellationToken),
            secondStore.ConfirmAsync(draft with
            {
                Explanation = "A competing explanation.",
                ConfirmationId = Guid.CreateVersion7(),
            }, TestContext.Current.CancellationToken));

        Assert.Single(results, result => result.Status == AuditComplementConfirmationWriteStatus.Accepted);
        Assert.Single(results, result => result.Status == AuditComplementConfirmationWriteStatus.Conflict);
        await using var verifyContext = CreateDbContext(tenantId);
        Assert.Equal(1, await verifyContext.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_AllowsAKeyToBeReusedAfterItsWindowExpires))]
    public async Task AuditComplementConfirmation_AllowsAKeyToBeReusedAfterItsWindowExpires()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var dbContext = CreateDbContext(tenantId);
        var protector = CreateProtector();
        var expiredAt = draft.ConfirmedAt.AddHours(-25);
        dbContext.AuditComplementIdempotencyRecords.Add(AuditComplementIdempotencyRecord.Create(
            draft.TenantId,
            draft.ActorId,
            draft.IdempotencyKey,
            protector.ComputeFingerprint(draft.RecordId, draft.Explanation),
            Guid.CreateVersion7(expiredAt),
            expiredAt));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var store = CreateStore(dbContext, protector);

        var result = await store.ConfirmAsync(draft, TestContext.Current.CancellationToken);

        Assert.Equal(AuditComplementConfirmationWriteStatus.Accepted, result.Status);
        Assert.Equal(draft.ConfirmationId, result.ConfirmationId);
        Assert.Equal(1, await dbContext.AuditComplementIdempotencyRecords.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
        Assert.Equal(1, await dbContext.OutboxMessages.IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_RejectsBlankOrOversizedExplanationsBeforeStorage))]
    public async Task AuditComplementConfirmation_RejectsBlankOrOversizedExplanationsBeforeStorage()
    {
        var store = new RecordingStore();
        var useCase = new ConfirmAuditRecordComplement(
            store,
            new ConfirmAuditRecordComplementInputValidator(),
            TimeProvider.System);
        var input = new ConfirmAuditRecordComplementInput(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "   ");

        var blank = await useCase.ExecuteAsync(input, TestContext.Current.CancellationToken);
        var oversized = await useCase.ExecuteAsync(input with { Explanation = new string('x', 1001) }, TestContext.Current.CancellationToken);

        Assert.Equal(ConfirmAuditRecordComplementStatus.InvalidExplanation, blank.Status);
        Assert.Equal(ConfirmAuditRecordComplementStatus.InvalidExplanation, oversized.Status);
        Assert.Equal(0, store.CallCount);
    }

    [Fact(DisplayName = nameof(AuditComplementConfirmation_AuthenticatesMessageAndTenantMetadataDuringDecryption))]
    public async Task AuditComplementConfirmation_AuthenticatesMessageAndTenantMetadataDuringDecryption()
    {
        var tenantId = Guid.CreateVersion7();
        var draft = CreateDraft(tenantId);
        await using var dbContext = CreateDbContext(tenantId);
        var protector = CreateProtector();
        await CreateStore(dbContext, protector).ConfirmAsync(draft, TestContext.Current.CancellationToken);
        var message = await dbContext.OutboxMessages.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == draft.ConfirmationId, TestContext.Current.CancellationToken);

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(message.Payload, message.Id, Guid.CreateVersion7(), message.PayloadKeyVersion));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(message.Payload, Guid.CreateVersion7(), message.TenantId, message.PayloadKeyVersion));
    }

    private BffAdminDbContext CreateDbContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);
        var options = new DbContextOptionsBuilder<BffAdminDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        return new BffAdminDbContext(options, tenantContext);
    }

    private static AuditComplementConfirmationStore CreateStore(
        BffAdminDbContext dbContext,
        OutboxPayloadProtector protector)
        => new(dbContext, new OutboxMessageWriter(dbContext, protector), protector);

    private static OutboxPayloadProtector CreateProtector()
        => new(Options.Create(new OutboxProtectionOptions { KeyBase64 = KeyBase64, KeyVersion = KeyVersion }));

    private static AuditComplementConfirmationDraft CreateDraft(Guid tenantId)
    {
        var confirmedAt = TimeProvider.System.GetUtcNow();
        return new AuditComplementConfirmationDraft(
            tenantId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(confirmedAt),
            Explanation,
            confirmedAt,
            null);
    }

    private sealed class RecordingStore : IAuditComplementConfirmationStore
    {
        public int CallCount { get; private set; }

        public Task<AuditComplementConfirmationWriteResult?> FindExistingAsync(
            AuditComplementConfirmationDraft draft,
            CancellationToken cancellationToken)
            => Task.FromResult<AuditComplementConfirmationWriteResult?>(null);

        public Task<AuditComplementConfirmationWriteResult> ConfirmAsync(
            AuditComplementConfirmationDraft draft,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new AuditComplementConfirmationWriteResult(
                AuditComplementConfirmationWriteStatus.Accepted,
                draft.ConfirmationId));
        }
    }
}
