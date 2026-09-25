using System.Text.Json;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Policies;
using CodeForCoders.Audit.Domain.ValueObjects;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.GrantStaffRole;
using CodeForCoders.Identity.Application.UseCases.Accounts.ListStaffMembers;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffRole;
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
public sealed class StaffRoleGrantRevokeTests(IdentityIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_ListsTenantMembersIncludingAccountsWithoutRoles))]
    public async Task StaffRoleGrantRevoke_ListsTenantMembersIncludingAccountsWithoutRoles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var noRoleId = await SeedInternalAsync(tenantId, "No Role", "norole@example.com", [], cancellationToken);
        await SeedInternalAsync(otherTenantId, "Other Tenant", "other@example.com", [], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var result = await new ListStaffMembers(
            new IdentityStaffAccountStore(dbContext),
            new ListStaffMembersInputValidator()).ExecuteAsync(
            new ListStaffMembersInput(tenantId, actorId, 1, 10),
            cancellationToken);

        Assert.Equal(2, result.Pagination.Total);
        Assert.Equal(["Admin", "No Role"], result.Data.Select(member => member.Name));
        Assert.True(result.Data[0].IsSelf);
        Assert.False(result.Data[1].IsSelf);
        Assert.Equal(noRoleId, result.Data[1].AccountId);
        Assert.Empty(result.Data[1].Roles);
        Assert.Equal("admin@example.com", result.Data[0].Email);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_GrantsRoleAndEmitsConformingAuditFact))]
    public async Task StaffRoleGrantRevoke_GrantsRoleAndEmitsConformingAuditFact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", [], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var result = await CreateGrantUseCase(dbContext).ExecuteAsync(
            GrantInput(tenantId, actorId, targetId, "suporte", "Cobertura temporária do atendimento.", "staff-role-grant-1"),
            cancellationToken);

        Assert.True(result.Changed);
        Assert.False(result.SessionsEnded);
        Assert.Equal(["suporte"], result.Member.Roles);
        Assert.Equal(1, await dbContext.IdempotencyRecords.CountAsync(cancellationToken));
        var audit = Assert.Single(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        var act = DeserializeAudit(audit);
        Assert.Equal("papel-concedido", act.Tipo);
        Assert.Equal("Cobertura temporária do atendimento.", act.Motivo);
        Assert.Equal(actorId, act.Autor?.Id);
        Assert.Equal(targetId, act.Alvo?.Id);
        Assert.Equal("suporte", act.Complemento!["papel"]);
        Assert.Empty(GetAuditPolicyReasons(act));
        Assert.Equal(9, JsonDocument.Parse(OutboxTestProtection.ReadPayload(audit)).RootElement.EnumerateObject().Count());
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_ReplaysTheSameRoleGrantWithoutAnotherAuditFact))]
    public async Task StaffRoleGrantRevoke_ReplaysTheSameRoleGrantWithoutAnotherAuditFact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", [], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateGrantUseCase(dbContext);
        var input = GrantInput(tenantId, actorId, targetId, "professor", "Acesso necessário à autoria.", "staff-role-replay");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);

        Assert.True(first.Changed);
        Assert.True(replay.Changed);
        Assert.Equal(first.Member.Roles, replay.Member.Roles);
        Assert.Single(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
        Assert.Single(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_ReturnsNoChangeForAnAlreadyGrantedRole))]
    public async Task StaffRoleGrantRevoke_ReturnsNoChangeForAnAlreadyGrantedRole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", [StaffRoleCatalog.Support], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var result = await CreateGrantUseCase(dbContext).ExecuteAsync(
            GrantInput(tenantId, actorId, targetId, "suporte", "Motivo sem efeito.", "staff-role-already-granted"),
            cancellationToken);

        Assert.False(result.Changed);
        Assert.False(result.SessionsEnded);
        Assert.Equal(["suporte"], result.Member.Roles);
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        Assert.Single(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_RevokesRoleAndAllSessionsInTheAuditedCommit))]
    public async Task StaffRoleGrantRevoke_RevokesRoleAndAllSessionsInTheAuditedCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", [StaffRoleCatalog.Teacher], cancellationToken);
        var sessionId = await SeedStaffSessionAsync(tenantId, targetId, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var result = await CreateRevokeUseCase(dbContext).ExecuteAsync(
            RevokeInput(tenantId, actorId, targetId, "professor", "Mudança de função.", "staff-role-revoke-1"),
            cancellationToken);

        Assert.True(result.Changed);
        Assert.True(result.SessionsEnded);
        Assert.Empty(result.Member.Roles);
        var session = await dbContext.StaffSessions.SingleAsync(item => item.Id == sessionId, cancellationToken);
        Assert.NotNull(session.RevokedOn);
        var audit = Assert.Single(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        var act = DeserializeAudit(audit);
        Assert.Equal("papel-revogado", act.Tipo);
        Assert.Equal("Mudança de função.", act.Motivo);
        Assert.Equal("professor", act.Complemento!["papel"]);
        Assert.Empty(GetAuditPolicyReasons(act));
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_RejectsChangingTheActorsOwnRoleWithoutWrites))]
    public async Task StaffRoleGrantRevoke_RejectsChangingTheActorsOwnRoleWithoutWrites()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<StaffRoleActionException>(() => CreateGrantUseCase(dbContext).ExecuteAsync(
            GrantInput(tenantId, actorId, actorId, "suporte", "Alteração proibida.", "staff-role-self"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("SELF_ROLE_CHANGE_FORBIDDEN", exception.Code);
        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_RejectsMissingAndStudentTargets))]
    public async Task StaffRoleGrantRevoke_RejectsMissingAndStudentTargets()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var studentId = await SeedStudentAsync(tenantId, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateGrantUseCase(dbContext);

        foreach (var targetId in new[] { Guid.CreateVersion7(), studentId })
        {
            var exception = await Assert.ThrowsAsync<StaffRoleActionException>(() => useCase.ExecuteAsync(
                GrantInput(tenantId, actorId, targetId, "suporte", "Acesso de suporte.", $"staff-role-target-{targetId:D}"),
                cancellationToken));
            Assert.Equal(404, exception.StatusCode);
            Assert.Equal("STAFF_MEMBER_NOT_FOUND", exception.Code);
        }

        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_RejectsBlankReasonAndReasonAboveContractLimit))]
    public async Task StaffRoleGrantRevoke_RejectsBlankReasonAndReasonAboveContractLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", [], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateGrantUseCase(dbContext);

        var exception = await Assert.ThrowsAsync<StaffRoleActionException>(() => useCase.ExecuteAsync(
            GrantInput(tenantId, actorId, targetId, "suporte", "   ", "staff-role-blank-reason"),
            cancellationToken));
        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("REASON_REQUIRED", exception.Code);

        var validation = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => useCase.ExecuteAsync(
            GrantInput(tenantId, actorId, targetId, "suporte", new string('m', 1001), "staff-role-long-reason"),
            cancellationToken));
        Assert.Contains(validation.Errors, error => error.PropertyName == nameof(StaffRoleActionCommand.Reason));
        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_RejectsAnIdempotencyKeyReusedWithAnotherRequest))]
    public async Task StaffRoleGrantRevoke_RejectsAnIdempotencyKeyReusedWithAnotherRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", [], cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateGrantUseCase(dbContext);
        await useCase.ExecuteAsync(
            GrantInput(tenantId, actorId, targetId, "suporte", "Motivo inicial.", "staff-role-key-conflict"),
            cancellationToken);

        var exception = await Assert.ThrowsAsync<StaffRoleActionException>(() => useCase.ExecuteAsync(
            GrantInput(tenantId, actorId, targetId, "suporte", "Outro motivo.", "staff-role-key-conflict"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("IDEMPOTENCY_CONFLICT", exception.Code);
        Assert.Single(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_ConcurrentGrantsCreateOneAssignmentAndOneAuditFact))]
    public async Task StaffRoleGrantRevoke_ConcurrentGrantsCreateOneAssignmentAndOneAuditFact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "Admin", "admin@example.com", [StaffRoleCatalog.Administrator], cancellationToken);
        var targetId = await SeedInternalAsync(tenantId, "Marina", "marina@example.com", [], cancellationToken);
        var barrier = new RoleAssignmentReadBarrier();
        await using var firstContext = fixture.CreateDbContext(tenantId);
        await using var secondContext = fixture.CreateDbContext(tenantId);
        var first = CreateGrantUseCase(firstContext, barrier.Wrap(new IdentityStaffAccountStore(firstContext)));
        var second = CreateGrantUseCase(secondContext, barrier.Wrap(new IdentityStaffAccountStore(secondContext)));

        var results = await Task.WhenAll(
            first.ExecuteAsync(GrantInput(tenantId, actorId, targetId, "suporte", "Cobertura de atendimento.", "staff-role-concurrent-1"), cancellationToken),
            second.ExecuteAsync(GrantInput(tenantId, actorId, targetId, "suporte", "Cobertura de atendimento.", "staff-role-concurrent-2"), cancellationToken));

        Assert.Single(results, result => result.Changed);
        Assert.Single(results, result => !result.Changed);
        await using var verificationContext = fixture.CreateDbContext(tenantId);
        Assert.Single(await verificationContext.StaffRoleAssignments
            .Where(assignment => assignment.AccountId == targetId && assignment.Role == "suporte")
            .ToListAsync(cancellationToken));
        Assert.Single(await verificationContext.OutboxMessages
            .Where(message => message.RoutingKey == "auditoria.ato-praticado.v1")
            .ToListAsync(cancellationToken));
        Assert.Equal(2, await verificationContext.IdempotencyRecords.CountAsync(cancellationToken));
    }

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

    private async Task<Guid> SeedStudentAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var accountId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(Account.CreateStudent(
            accountId,
            tenantId,
            "Student",
            "student@example.com",
            "student@example.com"));
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

    private static GrantStaffRole CreateGrantUseCase(
        IdentityDbContext dbContext,
        IIdentityStaffAccountStore? staffAccountStore = null)
        => new(CreateExecutor(dbContext, staffAccountStore));

    private static RevokeStaffRole CreateRevokeUseCase(IdentityDbContext dbContext)
        => new(CreateExecutor(dbContext));

    private static StaffRoleActionExecutor CreateExecutor(
        IdentityDbContext dbContext,
        IIdentityStaffAccountStore? staffAccountStore = null)
    {
        var destinations = Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            NotificationExchange = "notification.events.default",
            AuditExchange = "audit.events",
        });
        return new StaffRoleActionExecutor(
            staffAccountStore ?? new IdentityStaffAccountStore(dbContext),
            new IdentitySessionStore(dbContext),
            new StaffRoleMessageWriter(
                new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector),
                destinations),
            new IdentityUnitOfWork(dbContext),
            new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
            {
                FingerprintKeyBase64 = Convert.ToBase64String(
                    Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
            })),
            TimeProvider.System,
            new StaffRoleActionCommandValidator(),
            new StaffRoleChangeCommandValidator());
    }

    private static GrantStaffRoleInput GrantInput(
        Guid tenantId,
        Guid actorAccountId,
        Guid targetAccountId,
        string role,
        string reason,
        string idempotencyKey)
        => new(tenantId, actorAccountId, targetAccountId, role, reason, idempotencyKey);

    private static RevokeStaffRoleInput RevokeInput(
        Guid tenantId,
        Guid actorAccountId,
        Guid targetAccountId,
        string role,
        string reason,
        string idempotencyKey)
        => new(tenantId, actorAccountId, targetAccountId, role, reason, idempotencyKey);

    private static async Task AssertNoSideEffectsAsync(IdentityDbContext dbContext, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    private sealed class RoleAssignmentReadBarrier
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public IIdentityStaffAccountStore Wrap(IIdentityStaffAccountStore inner)
            => new BarrierStaffAccountStore(inner, this);

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _ready.TrySetResult();
            }

            await _ready.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class BarrierStaffAccountStore(
        IIdentityStaffAccountStore inner,
        RoleAssignmentReadBarrier barrier) : IIdentityStaffAccountStore
    {
        public Task<int> CountInternalStaffMembersAsync(Guid tenantId, CancellationToken cancellationToken)
            => inner.CountInternalStaffMembersAsync(tenantId, cancellationToken);

        public Task<IReadOnlyList<StaffMemberRecord>> ListInternalStaffMembersAsync(
            Guid tenantId,
            int page,
            int size,
            CancellationToken cancellationToken)
            => inner.ListInternalStaffMembersAsync(tenantId, page, size, cancellationToken);

        public Task<Account?> FindActiveAccountByNormalizedEmailAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken)
            => inner.FindActiveAccountByNormalizedEmailAsync(tenantId, normalizedEmail, cancellationToken);

        public Task<bool> HasAdministratorAsync(Guid tenantId, CancellationToken cancellationToken)
            => inner.HasAdministratorAsync(tenantId, cancellationToken);

        public Task<Account?> FindInternalAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
            => inner.FindInternalAccountAsync(tenantId, accountId, cancellationToken);

        public async Task<StaffRoleAssignment?> FindRoleAssignmentAsync(
            Guid tenantId,
            Guid accountId,
            string role,
            CancellationToken cancellationToken)
        {
            var assignment = await inner.FindRoleAssignmentAsync(tenantId, accountId, role, cancellationToken);
            if (assignment is null)
            {
                await barrier.WaitAsync(cancellationToken);
            }

            return assignment;
        }

        public Task<List<StaffSession>> FindUnrevokedStaffSessionsAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
            => inner.FindUnrevokedStaffSessionsAsync(tenantId, accountId, cancellationToken);

        public void AddAccount(Account account) => inner.AddAccount(account);

        public void AddCredential(Credential credential) => inner.AddCredential(credential);

        public void AddRoleAssignment(StaffRoleAssignment assignment) => inner.AddRoleAssignment(assignment);

        public void RemoveRoleAssignment(StaffRoleAssignment assignment) => inner.RemoveRoleAssignment(assignment);
    }
}
