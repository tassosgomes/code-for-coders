using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.AcceptStaffInvitation;
using CodeForCoders.Identity.Application.UseCases.Accounts.LookupStaffInvitation;
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
public sealed class StaffInvitationAcceptanceTests(IdentityIntegrationFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 12, 40, TimeSpan.Zero);
    private const string InvitationToken = "inv_Qm4Zt8Lw2Rp6Xa0c";

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_LookupReturnsOfferedRoleAndValidityWithoutWriting))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_LookupReturnsOfferedRoleAndValidityWithoutWriting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var expiresAt = Now.AddDays(7);
        await SeedInvitationAsync(tenantId, InvitationToken, StaffRoleCatalog.Teacher, expiresAt, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var preview = await CreateLookupUseCase(dbContext).ExecuteAsync(
            new LookupStaffInvitationInput(tenantId, InvitationToken),
            cancellationToken);

        Assert.Equal(StaffRoleCatalog.Teacher, preview.OfferedRole);
        Assert.Equal(expiresAt, preview.ExpiresAt);
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_LookupUsesTheSameErrorForMissingExpiredAcceptedAndSupersededTokens))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_LookupUsesTheSameErrorForMissingExpiredAcceptedAndSupersededTokens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInvitationAsync(tenantId, "expired-token", StaffRoleCatalog.Teacher, Now, cancellationToken, Now.AddDays(-8));
        await SeedInvitationAsync(tenantId, "accepted-token", StaffRoleCatalog.Teacher, Now.AddDays(7), cancellationToken, accepted: true);
        await SeedInvitationAsync(tenantId, "superseded-token", StaffRoleCatalog.Teacher, Now.AddDays(7), cancellationToken, superseded: true);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateLookupUseCase(dbContext);

        var exceptions = new List<StaffInvitationException>();
        foreach (var token in new[] { "missing-token", "expired-token", "accepted-token", "superseded-token" })
        {
            exceptions.Add(await Assert.ThrowsAsync<StaffInvitationException>(() => useCase.ExecuteAsync(
                new LookupStaffInvitationInput(tenantId, token),
                cancellationToken)));
        }

        Assert.All(exceptions, exception =>
        {
            Assert.Equal(422, exception.StatusCode);
            Assert.Equal("INVITATION_INVALID", exception.Code);
            Assert.Equal(exceptions[0].Title, exception.Title);
        });
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_CreatesAccountCredentialRoleSessionAndAuditInOneCommit))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_CreatesAccountCredentialRoleSessionAndAuditInOneCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var invitationId = await SeedInvitationAsync(
            tenantId,
            InvitationToken,
            StaffRoleCatalog.Teacher,
            Now.AddDays(7),
            cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var session = await CreateAcceptUseCase(dbContext).ExecuteAsync(
            Input(tenantId, InvitationToken, "Marina Alves", "Tr1lha!Segura", "accept-success"),
            cancellationToken);

        Assert.Equal("Marina Alves", session.Name);
        Assert.Equal([StaffRoleCatalog.Teacher], session.Roles);
        Assert.Equal([StaffRoleCatalog.ReadAuthoring], session.Permissions);
        Assert.True(session.ExpiresAt > Now);

        dbContext.ChangeTracker.Clear();
        var invitation = await dbContext.StaffInvitations.SingleAsync(item => item.Id == invitationId, cancellationToken);
        var account = await dbContext.Accounts.SingleAsync(item => item.Id == session.AccountId, cancellationToken);
        var credential = await dbContext.Credentials.SingleAsync(item => item.AccountId == session.AccountId, cancellationToken);
        var role = await dbContext.StaffRoleAssignments.SingleAsync(item => item.AccountId == session.AccountId, cancellationToken);
        var storedSession = await dbContext.StaffSessions.SingleAsync(item => item.Id == session.SessionId, cancellationToken);
        var audit = await dbContext.OutboxMessages.SingleAsync(
            message => message.RoutingKey == "auditoria.ato-praticado.v1",
            cancellationToken);
        using var auditDocument = JsonDocument.Parse(CreateOutboxPayloadProtector().Unprotect(audit));
        var auditPayload = auditDocument.RootElement;

        Assert.Equal(Now, invitation.AcceptedOn);
        Assert.Equal(AccountType.InternalActor, account.Type);
        Assert.True(account.IsConfirmed);
        Assert.Equal("guest@example.com", account.Email);
        Assert.Equal("guest@example.com", account.NormalizedEmail);
        Assert.True(new Pbkdf2PasswordHasher().Verify("Tr1lha!Segura", credential.PasswordHash));
        Assert.Equal(StaffRoleCatalog.Teacher, role.Role);
        Assert.Equal(session.SessionId, storedSession.Id);
        Assert.Equal("convite-interno-aceito", auditPayload.GetProperty("tipo").GetString());
        Assert.Equal(session.AccountId, auditPayload.GetProperty("autor").GetProperty("id").GetGuid());
        Assert.Equal(invitationId, auditPayload.GetProperty("alvo").GetProperty("id").GetGuid());
        Assert.False(auditPayload.TryGetProperty("motivo", out _));
        Assert.False(auditPayload.TryGetProperty("complemento", out _));
        Assert.Equal(7, auditPayload.EnumerateObject().Count());
        Assert.Empty(await dbContext.OutboxMessages.Where(message => message.RoutingKey == "notificacao.envio-solicitado.v1").ToListAsync(cancellationToken));
        Assert.Single(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_RejectsASecondUseWithTheGenericInvalidInvitationError))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_RejectsASecondUseWithTheGenericInvalidInvitationError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInvitationAsync(tenantId, InvitationToken, StaffRoleCatalog.Support, Now.AddDays(7), cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateAcceptUseCase(dbContext);

        await useCase.ExecuteAsync(Input(tenantId, InvitationToken, "Marina Alves", "Tr1lha!Segura", "accept-once"), cancellationToken);
        var exception = await Assert.ThrowsAsync<StaffInvitationException>(() => useCase.ExecuteAsync(
            Input(tenantId, InvitationToken, "Marina Alves", "Tr1lha!Segura", "accept-again"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("INVITATION_INVALID", exception.Code);
        Assert.Single(await dbContext.Accounts.ToListAsync(cancellationToken));
        Assert.Single(await dbContext.OutboxMessages.Where(message => message.RoutingKey == "auditoria.ato-praticado.v1").ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_RejectsAnExpiredInvitation))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_RejectsAnExpiredInvitation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInvitationAsync(tenantId, InvitationToken, StaffRoleCatalog.Teacher, Now, cancellationToken, Now.AddDays(-8));
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<StaffInvitationException>(() => CreateAcceptUseCase(dbContext).ExecuteAsync(
            Input(tenantId, InvitationToken, "Marina Alves", "Tr1lha!Segura", "accept-expired"),
            cancellationToken));

        Assert.Equal("INVITATION_INVALID", exception.Code);
        await AssertNoAcceptanceWritesAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_RejectsASupersededInvitation))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_RejectsASupersededInvitation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInvitationAsync(tenantId, InvitationToken, StaffRoleCatalog.Teacher, Now.AddDays(7), cancellationToken, superseded: true);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<StaffInvitationException>(() => CreateAcceptUseCase(dbContext).ExecuteAsync(
            Input(tenantId, InvitationToken, "Marina Alves", "Tr1lha!Segura", "accept-superseded"),
            cancellationToken));

        Assert.Equal("INVITATION_INVALID", exception.Code);
        await AssertNoAcceptanceWritesAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_RejectsAWeakPasswordWithoutWriting))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_RejectsAWeakPasswordWithoutWriting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInvitationAsync(tenantId, InvitationToken, StaffRoleCatalog.Teacher, Now.AddDays(7), cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<StaffInvitationException>(() => CreateAcceptUseCase(dbContext).ExecuteAsync(
            Input(tenantId, InvitationToken, "Marina Alves", "senha-fraca", "accept-weak-password"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("PASSWORD_POLICY_VIOLATION", exception.Code);
        await AssertNoAcceptanceWritesAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_RejectsAnEmailThatGainedAnAccountAfterIssuance))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_RejectsAnEmailThatGainedAnAccountAfterIssuance()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInvitationAsync(tenantId, InvitationToken, StaffRoleCatalog.Teacher, Now.AddDays(7), cancellationToken);
        await using (var seedContext = fixture.CreateDbContext(tenantId))
        {
            seedContext.Accounts.Add(Account.CreateStudent(
                Guid.CreateVersion7(Now.AddDays(-1)),
                tenantId,
                "Existing Student",
                "guest@example.com",
                "guest@example.com"));
            await new IdentityUnitOfWork(seedContext).CommitAsync(cancellationToken);
        }

        await using var dbContext = fixture.CreateDbContext(tenantId);
        var exception = await Assert.ThrowsAsync<StaffInvitationException>(() => CreateAcceptUseCase(dbContext).ExecuteAsync(
            Input(tenantId, InvitationToken, "Marina Alves", "Tr1lha!Segura", "accept-email-unavailable"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("INVITATION_EMAIL_UNAVAILABLE", exception.Code);
        await AssertNoAcceptanceWritesAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_ReplaysTheSameSessionForTheSameIdempotencyKey))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_ReplaysTheSameSessionForTheSameIdempotencyKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInvitationAsync(tenantId, InvitationToken, StaffRoleCatalog.Support, Now.AddDays(7), cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateAcceptUseCase(dbContext);
        var input = Input(tenantId, InvitationToken, "Marina Alves", "Tr1lha!Segura", "accept-replay");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);

        Assert.Equal(first.SessionId, replay.SessionId);
        Assert.Equal(first.AccountId, replay.AccountId);
        Assert.Single(await dbContext.Accounts.ToListAsync(cancellationToken));
        Assert.Single(await dbContext.StaffSessions.ToListAsync(cancellationToken));
        Assert.Single(await dbContext.OutboxMessages.Where(message => message.RoutingKey == "auditoria.ato-praticado.v1").ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_RejectsANameLongerThanTheContractLimitWithoutWriting))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task StaffInvitationAcceptance_RejectsANameLongerThanTheContractLimitWithoutWriting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInvitationAsync(tenantId, InvitationToken, StaffRoleCatalog.Teacher, Now.AddDays(7), cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => CreateAcceptUseCase(dbContext).ExecuteAsync(
            Input(tenantId, InvitationToken, new string('n', 201), "Tr1lha!Segura", "accept-long-name"),
            cancellationToken));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(AcceptStaffInvitationInput.Name));
        await AssertNoAcceptanceWritesAsync(dbContext, cancellationToken);
    }

    private async Task<Guid> SeedInvitationAsync(
        Guid tenantId,
        string rawToken,
        string role,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken,
        DateTimeOffset? invitedAt = null,
        bool accepted = false,
        bool superseded = false)
    {
        var issuedOn = invitedAt ?? Now.AddHours(-1);
        var invitation = StaffInvitation.Create(
            Guid.CreateVersion7(issuedOn),
            tenantId,
            "guest@example.com",
            "guest@example.com",
            role,
            HashToken(rawToken),
            issuedOn,
            expiresAt);
        if (accepted)
        {
            invitation.Accept(Now.AddMinutes(-1));
        }

        if (superseded)
        {
            invitation.Supersede(Now.AddMinutes(-1));
        }

        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.StaffInvitations.Add(invitation);
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        return invitation.Id;
    }

    private static LookupStaffInvitation CreateLookupUseCase(IdentityDbContext dbContext)
        => new(
            new IdentityStaffInvitationStore(dbContext),
            new FixedTimeProvider(Now),
            new LookupStaffInvitationInputValidator());

    private static AcceptStaffInvitation CreateAcceptUseCase(IdentityDbContext dbContext)
    {
        var destinations = Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            AuditExchange = "audit.events",
            NotificationExchange = "notification.events.default",
        });
        var messageWriter = new StaffInvitationMessageWriter(
            new OutboxMessageWriter(dbContext, destinations, CreateOutboxPayloadProtector()),
            Options.Create(new StaffInvitationOptions()),
            destinations);
        return new AcceptStaffInvitation(
            new IdentityStaffAccountStore(dbContext),
            new IdentityStaffInvitationStore(dbContext),
            new IdentitySessionStore(dbContext),
            messageWriter,
            new IdentityUnitOfWork(dbContext),
            CreateFingerprinter(),
            new Pbkdf2PasswordHasher(),
            Options.Create(new StaffSessionOptions { InactivityTimeoutMinutes = 60 }),
            new FixedTimeProvider(Now),
            new AcceptStaffInvitationInputValidator());
    }

    private static AcceptStaffInvitationInput Input(
        Guid tenantId,
        string token,
        string name,
        string password,
        string idempotencyKey)
        => new(tenantId, token, name, password, idempotencyKey);

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static IIdempotencyFingerprinter CreateFingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(
                Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));

    private static OutboxPayloadProtector CreateOutboxPayloadProtector()
        => new(Options.Create(new OutboxProtectionOptions
        {
            KeyBase64 = Convert.ToBase64String(
                Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
        }));

    private static async Task AssertNoAcceptanceWritesAsync(
        IdentityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Assert.Empty(await dbContext.Credentials.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.StaffRoleAssignments.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.StaffSessions.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
