using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.AcceptStaffInvitation;
using CodeForCoders.Identity.Application.UseCases.Accounts.CreateStaffInvitation;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Configuration;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

/// <summary>
/// RACE #7 - "Concurrent acceptance may grant the role of an already-superseded invitation".
///
/// Two isolated DbContext scopes. Scope A (acceptance) loads the original invitation, judges it
/// acceptable and the email still free, then parks on a barrier placed right after that check.
/// While A is parked, scope B issues a new staff invitation for the SAME normalized email with a
/// different idempotency key, which supersedes the original invitation and commits. A is released
/// and finishes.
///
/// Regression guard for the fix (xmin optimistic concurrency token on StaffInvitation): the
/// supersession bumps the invitation's row version, so A's acceptance UPDATE affects zero rows and
/// the commit surfaces a concurrency conflict. The existing <c>catch (ConcurrentWriteException)</c>
/// path in AcceptStaffInvitation re-reads the invitation, sees it is no longer acceptable, and
/// rejects with INVITATION_INVALID while the atomic SaveChanges rolls back every write. No
/// account, role or session leaks for the superseded (administrator) invitation, even with the
/// deterministic barrier holding the window open.
/// </summary>
[Collection(IdentityIntegrationCollection.Name)]
public sealed class StaffInvitationAcceptanceSupersessionRaceTests(IdentityIntegrationFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 12, 40, TimeSpan.Zero);
    private const string Email = "race7-guest@example.com";
    private const string OriginalToken = "inv_race7_original_token_value";

    [Fact(DisplayName = nameof(Race7_ConcurrentAcceptanceAfterSupersession_IsRejectedAndLeaksNothing))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task Race7_ConcurrentAcceptanceAfterSupersession_IsRejectedAndLeaksNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7(Now);
        var originalInvitationId = await SeedInvitationAsync(
            tenantId,
            OriginalToken,
            StaffRoleCatalog.Administrator,
            Now.AddDays(7),
            cancellationToken);

        var barrier = new ConcurrencyBarrier();

        // Scope A: acceptance of the ORIGINAL (administrator) token, parked after the email check.
        await using var acceptContext = fixture.CreateDbContext(tenantId);
        var acceptInput = new AcceptStaffInvitationInput(
            tenantId,
            OriginalToken,
            "Marina Alves",
            "Tr1lha!Segura",
            "race7-accept");
        var acceptTask = Task.Run(
            async () => await CreateAcceptUseCase(acceptContext, barrier).ExecuteAsync(acceptInput, cancellationToken),
            cancellationToken);

        await barrier.Reached;

        // Scope B: issue a SUPPORT invitation for the same email -> supersedes the original.
        await using (var issueContext = fixture.CreateDbContext(tenantId))
        {
            var issued = await CreateIssueUseCase(issueContext).ExecuteAsync(
                new CreateStaffInvitationInput(
                    tenantId,
                    actorId,
                    Email,
                    StaffRoleCatalog.Support,
                    "Reassigned to support",
                    "race7-issue"),
                cancellationToken);
            Assert.Equal(originalInvitationId, issued.SupersededInvitationId);
        }

        barrier.Release();

        AcceptStaffInvitationOutput? session = null;
        StaffInvitationException? rejection = null;
        try
        {
            session = await acceptTask;
        }
        catch (StaffInvitationException exception)
        {
            rejection = exception;
        }

        await using var verify = fixture.CreateDbContext(tenantId);
        var original = await verify.StaffInvitations.AsNoTracking()
            .SingleAsync(item => item.Id == originalInvitationId, cancellationToken);
        var support = await verify.StaffInvitations.AsNoTracking()
            .SingleAsync(item => item.OfferedRole == StaffRoleCatalog.Support, cancellationToken);
        var accounts = await verify.Accounts.AsNoTracking().ToListAsync(cancellationToken);
        var roleAssignments = await verify.StaffRoleAssignments.AsNoTracking().ToListAsync(cancellationToken);
        var staffSessions = await verify.StaffSessions.AsNoTracking().ToListAsync(cancellationToken);

        TestContext.Current.SendDiagnosticMessage(
            "RACE7 outcome: accepted={0} rejectedCode={1} originalAccepted={2} originalSuperseded={3} "
            + "accounts={4} roles=[{5}] staffSessions={6} supportPending={7}",
            session is not null,
            rejection?.Code ?? "<none>",
            original.AcceptedOn,
            original.SupersededOn,
            accounts.Count,
            string.Join(",", roleAssignments.Select(role => role.Role)),
            staffSessions.Count,
            support.AcceptedOn is null && support.SupersededOn is null);

        // The race is PREVENTED: the parked acceptance loses to the supersession and is rejected.
        Assert.True(
            session is null,
            "Expected the parked acceptance to be rejected, but it succeeded and granted a role for a "
            + "superseded invitation (race NOT closed).");
        Assert.NotNull(rejection);
        Assert.Equal(422, rejection!.StatusCode);
        Assert.Equal("INVITATION_INVALID", rejection.Code);

        // Nothing leaked: no account, credential, role assignment or session was persisted.
        Assert.Empty(accounts);
        Assert.Empty(roleAssignments);
        Assert.Empty(staffSessions);
        Assert.Empty(await verify.Credentials.AsNoTracking().ToListAsync(cancellationToken));

        // The original invitation is superseded and was NOT accepted.
        Assert.Null(original.AcceptedOn);
        Assert.NotNull(original.SupersededOn);

        // The fresh support offer remains the single pending invitation for the email.
        Assert.Null(support.AcceptedOn);
        Assert.Null(support.SupersededOn);
        Assert.Equal(StaffRoleCatalog.Support, support.OfferedRole);
    }

    [Fact(DisplayName = nameof(Race7_SupersessionBeforeAcceptanceStarts_RejectsTheOldToken))]
    [Trait("Layer", "Identity staff invitation acceptance - Integration")]
    public async Task Race7_SupersessionBeforeAcceptanceStarts_RejectsTheOldToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7(Now);
        var originalInvitationId = await SeedInvitationAsync(
            tenantId,
            OriginalToken,
            StaffRoleCatalog.Administrator,
            Now.AddDays(7),
            cancellationToken);

        // Supersession completes BEFORE acceptance begins (the control, no interleaving).
        await using (var issueContext = fixture.CreateDbContext(tenantId))
        {
            var issued = await CreateIssueUseCase(issueContext).ExecuteAsync(
                new CreateStaffInvitationInput(
                    tenantId,
                    actorId,
                    Email,
                    StaffRoleCatalog.Support,
                    "Reassigned to support",
                    "race7-control-issue"),
                cancellationToken);
            Assert.Equal(originalInvitationId, issued.SupersededInvitationId);
        }

        await using var acceptContext = fixture.CreateDbContext(tenantId);
        var exception = await Assert.ThrowsAsync<StaffInvitationException>(() =>
            CreateAcceptUseCase(acceptContext, new ConcurrencyBarrier()).ExecuteAsync(
                new AcceptStaffInvitationInput(
                    tenantId,
                    OriginalToken,
                    "Marina Alves",
                    "Tr1lha!Segura",
                    "race7-control-accept"),
                cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("INVITATION_INVALID", exception.Code);

        await using var verify = fixture.CreateDbContext(tenantId);
        Assert.Empty(await verify.Accounts.AsNoTracking().ToListAsync(cancellationToken));
        Assert.Empty(await verify.StaffRoleAssignments.AsNoTracking().ToListAsync(cancellationToken));
        Assert.Empty(await verify.StaffSessions.AsNoTracking().ToListAsync(cancellationToken));
    }

    private async Task<Guid> SeedInvitationAsync(
        Guid tenantId,
        string rawToken,
        string role,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var invitation = StaffInvitation.Create(
            Guid.CreateVersion7(Now.AddHours(-1)),
            tenantId,
            Email,
            Email,
            role,
            HashToken(rawToken),
            Now.AddHours(-1),
            expiresAt);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.StaffInvitations.Add(invitation);
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        return invitation.Id;
    }

    private static AcceptStaffInvitation CreateAcceptUseCase(IdentityDbContext dbContext, ConcurrencyBarrier barrier)
    {
        var destinations = Destinations();
        var messageWriter = new StaffInvitationMessageWriter(
            new OutboxMessageWriter(dbContext, destinations, Protector()),
            Options.Create(new StaffInvitationOptions()),
            destinations);
        return new AcceptStaffInvitation(
            new BarrieredStaffAccountStore(new IdentityStaffAccountStore(dbContext), barrier),
            new IdentityStaffInvitationStore(dbContext),
            new IdentitySessionStore(dbContext),
            messageWriter,
            new IdentityUnitOfWork(dbContext),
            Fingerprinter(),
            new Pbkdf2PasswordHasher(),
            Options.Create(new StaffSessionOptions { InactivityTimeoutMinutes = 60 }),
            new FixedTimeProvider(Now),
            new AcceptStaffInvitationInputValidator());
    }

    private static CreateStaffInvitation CreateIssueUseCase(IdentityDbContext dbContext)
    {
        var destinations = Destinations();
        var messageWriter = new StaffInvitationMessageWriter(
            new OutboxMessageWriter(dbContext, destinations, Protector()),
            Options.Create(new StaffInvitationOptions()),
            destinations);
        return new CreateStaffInvitation(
            new IdentityStaffAccountStore(dbContext),
            new IdentityStaffInvitationStore(dbContext),
            new IdentitySessionStore(dbContext),
            messageWriter,
            new IdentityUnitOfWork(dbContext),
            Fingerprinter(),
            Options.Create(new StaffInvitationOptions()),
            new FixedTimeProvider(Now),
            new CreateStaffInvitationInputValidator());
    }

    private static IOptions<OutboxDestinationOptions> Destinations()
        => Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            AuditExchange = "audit.events",
            NotificationExchange = "notification.events.default",
        });

    private static IIdempotencyFingerprinter Fingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(
                Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));

    private static OutboxPayloadProtector Protector()
        => new(Options.Create(new OutboxProtectionOptions
        {
            KeyBase64 = Convert.ToBase64String(
                Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
        }));

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
