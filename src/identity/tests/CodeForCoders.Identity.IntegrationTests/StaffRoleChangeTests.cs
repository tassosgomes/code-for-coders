using System.Text.Json;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Policies;
using CodeForCoders.Audit.Domain.ValueObjects;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStaffRole;
using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Configuration;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StaffRoleChangeTests(IdentityIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(StaffRoleChange_ReplacesTheRoleAndAppendsTwoCorrelatedAuditFacts))]
    [Trait("Layer", "Identity staff role change - Integration")]
    public async Task StaffRoleChange_ReplacesTheRoleAndAppendsTwoCorrelatedAuditFacts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(
            tenantId,
            "Administrator",
            "administrator@example.com",
            [StaffRoleCatalog.Administrator],
            cancellationToken);
        var targetId = await SeedInternalAsync(
            tenantId,
            "Marina",
            "marina@example.com",
            ["professor"],
            cancellationToken);
        var sessionId = await SeedStaffSessionAsync(tenantId, targetId, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            ChangeInput(tenantId, actorId, targetId, "professor", "financeiro", "Mudou de função para o time financeiro.", "staff-role-change-success"),
            cancellationToken);

        Assert.Equal(["financeiro"], result.Member.Roles);
        Assert.True(result.Changed);
        Assert.True(result.SessionsEnded);

        await using var verificationContext = fixture.CreateDbContext(tenantId);
        var roles = await verificationContext.StaffRoleAssignments
            .Where(assignment => assignment.AccountId == targetId)
            .Select(assignment => assignment.Role)
            .ToListAsync(cancellationToken);
        Assert.Equal(["financeiro"], roles);
        var session = await verificationContext.StaffSessions.SingleAsync(
            item => item.Id == sessionId,
            cancellationToken);
        Assert.NotNull(session.RevokedOn);

        var messages = await verificationContext.OutboxMessages
            .Where(message => message.RoutingKey == "auditoria.ato-praticado.v1")
            .ToListAsync(cancellationToken);
        Assert.Equal(2, messages.Count);
        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message.CorrelationId)));
        Assert.Equal(messages[0].CorrelationId, messages[1].CorrelationId);

        var revokedMessage = Assert.Single(messages, message => DeserializeAudit(message).Tipo == "papel-revogado");
        var grantedMessage = Assert.Single(messages, message => DeserializeAudit(message).Tipo == "papel-concedido");
        var revoked = DeserializeAudit(revokedMessage);
        var granted = DeserializeAudit(grantedMessage);
        Assert.NotEqual(revoked.FatoId, granted.FatoId);
        Assert.Equal(actorId, revoked.Autor!.Id);
        Assert.Equal(actorId, granted.Autor!.Id);
        Assert.Equal(targetId, revoked.Alvo!.Id);
        Assert.Equal(targetId, granted.Alvo!.Id);
        Assert.Equal("professor", revoked.Complemento!["papel"]);
        Assert.Equal("financeiro", granted.Complemento!["papel"]);
        Assert.Equal("Mudou de função para o time financeiro.", revoked.Motivo);
        Assert.Equal(revoked.Motivo, granted.Motivo);
        Assert.Equal(revoked.PraticadoEm, granted.PraticadoEm);
        Assert.Empty(GetAuditPolicyReasons(revoked));
        Assert.Empty(GetAuditPolicyReasons(granted));
        Assert.All(messages, message =>
        {
            using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(message));
            Assert.False(payload.RootElement.TryGetProperty("correlationId", out _));
            Assert.False(payload.RootElement.TryGetProperty("CorrelationId", out _));
        });
    }

    [Fact(DisplayName = nameof(StaffRoleChange_DestinationAlreadyHeldCreatesOnlyTheRevocationFact))]
    [Trait("Layer", "Identity staff role change - Integration")]
    public async Task StaffRoleChange_DestinationAlreadyHeldCreatesOnlyTheRevocationFact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Administrator", "administrator@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", ["professor", "financeiro"], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            ChangeInput(tenantId, actorId, targetId, "professor", "financeiro", "Consolidando o acesso financeiro.", "staff-role-change-existing-destination"),
            cancellationToken);

        Assert.Equal(["financeiro"], result.Member.Roles);
        Assert.True(result.Changed);
        Assert.True(result.SessionsEnded);
        var message = Assert.Single(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        var audit = DeserializeAudit(message);
        Assert.Equal("papel-revogado", audit.Tipo);
        Assert.Equal("professor", audit.Complemento!["papel"]);
        Assert.Empty(GetAuditPolicyReasons(audit));
    }

    [Fact(DisplayName = nameof(StaffRoleChange_FailureBeforeCommitLeavesTheRoleSessionAndOutboxUnchanged))]
    [Trait("Layer", "Identity staff role change - Integration")]
    public async Task StaffRoleChange_FailureBeforeCommitLeavesTheRoleSessionAndOutboxUnchanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Administrator", "administrator@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", ["professor"], cancellationToken);
        var sessionId = await SeedStaffSessionAsync(tenantId, targetId, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var failingWriter = new FailAfterRevocationWriter(CreateMessageWriter(dbContext));
        var useCase = CreateUseCase(dbContext, failingWriter);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            ChangeInput(tenantId, actorId, targetId, "professor", "financeiro", "Mudou para o time financeiro.", "staff-role-change-forced-failure"),
            cancellationToken));

        await using var verificationContext = fixture.CreateDbContext(tenantId);
        var roles = await verificationContext.StaffRoleAssignments
            .Where(assignment => assignment.AccountId == targetId)
            .Select(assignment => assignment.Role)
            .ToListAsync(cancellationToken);
        Assert.Equal(["professor"], roles);
        var session = await verificationContext.StaffSessions.SingleAsync(
            item => item.Id == sessionId,
            cancellationToken);
        Assert.Null(session.RevokedOn);
        Assert.Empty(await verificationContext.OutboxMessages.ToListAsync(cancellationToken));
        Assert.Empty(await verificationContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffRoleChange_RejectsWhenTheSourceRoleIsNotHeld))]
    [Trait("Layer", "Identity staff role change - Integration")]
    public async Task StaffRoleChange_RejectsWhenTheSourceRoleIsNotHeld()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Administrator", "administrator@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", ["financeiro"], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<StaffRoleActionException>(() => CreateUseCase(dbContext).ExecuteAsync(
            ChangeInput(tenantId, actorId, targetId, "professor", "financeiro", "Troca inválida.", "staff-role-change-source-missing"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("ROLE_NOT_HELD", exception.Code);
        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffRoleChange_RejectsTheSameSourceAndDestinationRole))]
    [Trait("Layer", "Identity staff role change - Integration")]
    public async Task StaffRoleChange_RejectsTheSameSourceAndDestinationRole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Administrator", "administrator@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", ["professor"], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<StaffRoleActionException>(() => CreateUseCase(dbContext).ExecuteAsync(
            ChangeInput(tenantId, actorId, targetId, "professor", "professor", "Papel mantido.", "staff-role-change-same-role"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("ROLE_CHANGE_INVALID", exception.Code);
        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffRoleChange_RejectsChangingTheActorsOwnRoles))]
    [Trait("Layer", "Identity staff role change - Integration")]
    public async Task StaffRoleChange_RejectsChangingTheActorsOwnRoles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Administrator", "administrator@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<StaffRoleActionException>(() => CreateUseCase(dbContext).ExecuteAsync(
            ChangeInput(tenantId, actorId, actorId, "administrador", "financeiro", "Troca de acesso.", "staff-role-change-self"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("SELF_ROLE_CHANGE_FORBIDDEN", exception.Code);
        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffRoleChange_ReplaysTheSameRequestWithoutAppendingDuplicateFacts))]
    [Trait("Layer", "Identity staff role change - Integration")]
    public async Task StaffRoleChange_ReplaysTheSameRequestWithoutAppendingDuplicateFacts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Administrator", "administrator@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", ["professor"], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateUseCase(dbContext);
        var input = ChangeInput(tenantId, actorId, targetId, "professor", "financeiro", "Mudou para o financeiro.", "staff-role-change-replay");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);

        Assert.True(first.Changed);
        Assert.True(replay.Changed);
        Assert.Equal(first.Member.Roles, replay.Member.Roles);
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(cancellationToken));
        Assert.Single(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    private async Task<Guid> SeedInternalAsync(
        Guid tenantId,
        string name,
        string email,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var accountId = Guid.CreateVersion7(now);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(Account.CreateInternal(accountId, tenantId, name, email, email.ToLowerInvariant()));
        foreach (var role in roles)
        {
            dbContext.StaffRoleAssignments.Add(StaffRoleAssignment.Create(
                Guid.CreateVersion7(now), tenantId, accountId, role, now));
        }

        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        return accountId;
    }

    private async Task<Guid> SeedStaffSessionAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var session = StaffSession.Create(Guid.CreateVersion7(now), tenantId, accountId, now, now.AddHours(1));
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.StaffSessions.Add(session);
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        return session.Id;
    }

    private static ChangeStaffRole CreateUseCase(
        IdentityDbContext dbContext,
        IStaffRoleMessageWriter? messageWriter = null)
    {
        var destinations = Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            NotificationExchange = "notification.events.default",
            AuditExchange = "audit.events",
        });
        var executor = new StaffRoleActionExecutor(
            new IdentityStaffAccountStore(dbContext),
            new IdentitySessionStore(dbContext),
            messageWriter ?? CreateMessageWriter(dbContext, destinations),
            new IdentityUnitOfWork(dbContext),
            new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
            {
                FingerprintKeyBase64 = Convert.ToBase64String(
                    Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
            })),
            TimeProvider.System,
            new StaffRoleActionCommandValidator(),
            new StaffRoleChangeCommandValidator());
        return new ChangeStaffRole(executor);
    }

    private static StaffRoleMessageWriter CreateMessageWriter(
        IdentityDbContext dbContext,
        IOptions<OutboxDestinationOptions>? destinations = null)
    {
        var destinationOptions = destinations ?? Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            NotificationExchange = "notification.events.default",
            AuditExchange = "audit.events",
        });
        return new StaffRoleMessageWriter(
            new OutboxMessageWriter(dbContext, destinationOptions, OutboxTestProtection.Protector),
            destinationOptions);
    }

    private static ChangeStaffRoleInput ChangeInput(
        Guid tenantId,
        Guid actorId,
        Guid targetId,
        string fromRole,
        string toRole,
        string reason,
        string idempotencyKey)
        => new(tenantId, actorId, targetId, fromRole, toRole, reason, idempotencyKey);

    private static AtoPraticado DeserializeAudit(OutboxMessage message)
        => JsonSerializer.Deserialize<AtoPraticado>(OutboxTestProtection.ReadPayload(message))
            ?? throw new InvalidOperationException("The outbox audit payload was empty.");

    private static string[] GetAuditPolicyReasons(AtoPraticado act)
        => AdministrativeActPolicy.GetNonConformityReasons(new AdministrativeAct(
            act.FatoId,
            act.Origem ?? string.Empty,
            act.Tipo,
            act.TenantId,
            act.PraticadoEm,
            act.Autor is null ? null : new AdministrativeActReference(act.Autor.Tipo, act.Autor.Id),
            act.Alvo is null ? null : new AdministrativeActReference(act.Alvo.Tipo, act.Alvo.Id),
            act.Complemento,
            act.Motivo,
            act.ComplementoInvalido,
            act.ComplementoOriginalCanonico));

    private static async Task AssertNoSideEffectsAsync(IdentityDbContext dbContext, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    private sealed class FailAfterRevocationWriter(IStaffRoleMessageWriter inner) : IStaffRoleMessageWriter
    {
        public Task AppendRoleGrantedAsync(
            Guid tenantId,
            Guid actorAccountId,
            Guid targetAccountId,
            string role,
            string reason,
            DateTimeOffset practicedOn,
            string? correlationId,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("The forced failure occurs before the transaction commits.");

        public Task AppendRoleRevokedAsync(
            Guid tenantId,
            Guid actorAccountId,
            Guid targetAccountId,
            string role,
            string reason,
            DateTimeOffset practicedOn,
            string? correlationId,
            CancellationToken cancellationToken)
            => inner.AppendRoleRevokedAsync(
                tenantId,
                actorAccountId,
                targetAccountId,
                role,
                reason,
                practicedOn,
                correlationId,
                cancellationToken);
    }
}
