using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStaffPassword;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

/// <summary>
/// RACE #8 (staff variant) - "A concurrent staff login may escape the session revocation of a
/// password reset". Mirrors <see cref="StudentSessionPasswordResetRevocationRaceTests"/>.
///
/// Scope A (login) authenticates the staff member, verifies the hash and parks right before the new
/// staff session row is staged. Scope B runs the password reset, which rewrites the credential and
/// revokes every staff session it can see - A's does not exist yet. A is then released.
/// The safe outcome is that the login is rejected and no staff session is ever persisted.
/// </summary>
[Collection(IdentityIntegrationCollection.Name)]
public sealed class StaffSessionPasswordResetRevocationRaceTests(IdentityIntegrationFixture fixture)
{
    private static readonly DateTimeOffset ResetTime = new(2026, 10, 3, 9, 12, 40, TimeSpan.Zero);
    private static readonly DateTimeOffset LoginTime = ResetTime.AddSeconds(5);
    private const string Email = "race8-staff@example.com";
    private const string OldPassword = "SenhaForte1!";
    private const string NewPassword = "NovaSenha2!";
    private const string RecoveryPurpose = "recuperacao-de-senha";
    private const string RecoveryRawToken = "race8_staff_recovery_token_value";

    [Fact(DisplayName = nameof(Race8Staff_LoginConcurrentWithPasswordReset_DoesNotCreateAValidSession))]
    [Trait("Layer", "Identity staff session - Integration")]
    public async Task Race8Staff_LoginConcurrentWithPasswordReset_DoesNotCreateAValidSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var accountId = await SeedStaffWithRecoveryTokenAsync(tenantId, cancellationToken);

        var barrier = new ConcurrencyBarrier();

        // Scope A: staff login, parked just before the new session row is staged.
        await using var loginContext = fixture.CreateDbContext(tenantId);
        var loginTask = Task.Run(
            async () => await CreateLoginUseCase(
                    loginContext,
                    new BarrieredSessionStore(new IdentitySessionStore(loginContext), barrier, parkOnStaffSession: true))
                .ExecuteAsync(
                    new AuthenticateStaffSessionInput(tenantId, Email, OldPassword, "race8-staff-login"),
                    cancellationToken),
            cancellationToken);

        await barrier.Reached;

        // Scope B: reset the password (rewrites credential, revokes every visible session) and commit.
        await using (var resetContext = fixture.CreateDbContext(tenantId))
        {
            var reset = await CreateResetUseCase(resetContext).ExecuteAsync(
                new ResetStaffPasswordInput(tenantId, RecoveryRawToken, NewPassword, "race8-staff-reset"),
                cancellationToken);
            Assert.Equal(204, reset.StatusCode);
        }

        barrier.Release();

        StaffSessionException? rejection = null;
        AuthenticateStaffSessionOutput? login = null;
        try
        {
            login = await loginTask;
        }
        catch (StaffSessionException exception)
        {
            rejection = exception;
        }

        await using var verify = fixture.CreateDbContext(tenantId);
        var sessions = await verify.StaffSessions.AsNoTracking().ToListAsync(cancellationToken);
        var credential = await verify.Credentials.AsNoTracking().SingleAsync(cancellationToken);

        TestContext.Current.SendDiagnosticMessage(
            "RACE8-STAFF outcome: loginSucceeded={0} rejectedCode={1} sessionCount={2} "
            + "credentialMatchesNewPassword={3}",
            login is not null,
            rejection?.Code ?? "<none>",
            sessions.Count,
            new Pbkdf2PasswordHasher().Verify(NewPassword, credential.PasswordHash));

        Assert.True(new Pbkdf2PasswordHasher().Verify(NewPassword, credential.PasswordHash));
        Assert.False(new Pbkdf2PasswordHasher().Verify(OldPassword, credential.PasswordHash));

        Assert.True(
            login is null,
            "Expected the concurrent staff login to be rejected, but it minted a session that escaped the "
            + "password reset revocation (race NOT closed).");
        Assert.NotNull(rejection);
        Assert.Equal(401, rejection!.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", rejection.Code);
        Assert.DoesNotContain(sessions, session => session.AccountId == accountId);
    }

    /// <summary>
    /// Reverse interleaving: the login re-stages the credential under its xmin token, so a login that
    /// commits while a reset is in flight bumps the row version the reset read. The reset must still
    /// win - re-read and re-apply - and revoke that login's session.
    /// </summary>
    [Fact(DisplayName = nameof(Race8Staff_LoginCommittedDuringPasswordReset_ResetStillWinsAndRevokesIt))]
    [Trait("Layer", "Identity staff session - Integration")]
    public async Task Race8Staff_LoginCommittedDuringPasswordReset_ResetStillWinsAndRevokesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedStaffWithRecoveryTokenAsync(tenantId, cancellationToken);

        var barrier = new ConcurrencyBarrier();

        // Scope A: reset, parked after it rewrote the credential and loaded sessions, before commit.
        await using var resetContext = fixture.CreateDbContext(tenantId);
        var resetTask = Task.Run(
            async () => await CreateResetUseCase(
                    resetContext,
                    new BarrieredStaffAccountStore(
                        new IdentityStaffAccountStore(resetContext),
                        barrier,
                        parkAfterLoadingStaffSessions: true),
                    LoginTime.AddSeconds(5))
                .ExecuteAsync(
                    new ResetStaffPasswordInput(tenantId, RecoveryRawToken, NewPassword, "race8-staff-rev-reset"),
                    cancellationToken),
            cancellationToken);

        await barrier.Reached;

        // Scope B: a login with the old password commits meanwhile.
        Guid loginSessionId;
        await using (var loginContext = fixture.CreateDbContext(tenantId))
        {
            var login = await CreateLoginUseCase(loginContext, new IdentitySessionStore(loginContext)).ExecuteAsync(
                new AuthenticateStaffSessionInput(tenantId, Email, OldPassword, "race8-staff-rev-login"),
                cancellationToken);
            loginSessionId = login.SessionId;
        }

        barrier.Release();
        var reset = await resetTask;

        await using var verify = fixture.CreateDbContext(tenantId);
        var credential = await verify.Credentials.AsNoTracking().SingleAsync(cancellationToken);
        var loginSession = await verify.StaffSessions.AsNoTracking()
            .SingleAsync(item => item.Id == loginSessionId, cancellationToken);

        Assert.Equal(204, reset.StatusCode);
        Assert.True(new Pbkdf2PasswordHasher().Verify(NewPassword, credential.PasswordHash));
        Assert.NotNull(loginSession.RevokedOn);
    }

    [Fact(DisplayName = nameof(Race8Staff_PasswordResetAfterExistingSession_RevokesItAsExpected))]
    [Trait("Layer", "Identity staff session - Integration")]
    public async Task Race8Staff_PasswordResetAfterExistingSession_RevokesItAsExpected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var accountId = await SeedStaffWithRecoveryTokenAsync(tenantId, cancellationToken);

        Guid existingSessionId;
        await using (var seedSession = fixture.CreateDbContext(tenantId))
        {
            var session = StaffSession.Create(
                Guid.CreateVersion7(LoginTime),
                tenantId,
                accountId,
                LoginTime,
                LoginTime.AddMinutes(30));
            existingSessionId = session.Id;
            seedSession.StaffSessions.Add(session);
            await new IdentityUnitOfWork(seedSession).CommitAsync(cancellationToken);
        }

        await using (var resetContext = fixture.CreateDbContext(tenantId))
        {
            var reset = await CreateResetUseCase(resetContext).ExecuteAsync(
                new ResetStaffPasswordInput(tenantId, RecoveryRawToken, NewPassword, "race8-staff-control"),
                cancellationToken);
            Assert.Equal(204, reset.StatusCode);
        }

        await using var validateContext = fixture.CreateDbContext(tenantId);
        var validated = await new ValidateStaffSession(
                new IdentitySessionStore(validateContext),
                Options.Create(new StaffSessionOptions { InactivityTimeoutMinutes = 30 }),
                new FixedTimeProvider(LoginTime.AddMinutes(1)),
                new ValidateStaffSessionInputValidator())
            .ExecuteAsync(new ValidateStaffSessionInput(tenantId, existingSessionId), cancellationToken);

        await using var verify = fixture.CreateDbContext(tenantId);
        var storedSession = await verify.StaffSessions.AsNoTracking()
            .SingleAsync(item => item.Id == existingSessionId, cancellationToken);

        Assert.NotNull(storedSession.RevokedOn);
        Assert.Null(validated);
    }

    private async Task<Guid> SeedStaffWithRecoveryTokenAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var accountId = Guid.CreateVersion7(ResetTime.AddHours(-1));
        var account = Account.CreateInternal(accountId, tenantId, "Race Staff", Email, Email);
        var credential = Credential.Create(
            Guid.CreateVersion7(ResetTime.AddHours(-1).AddTicks(1)),
            tenantId,
            accountId,
            new Pbkdf2PasswordHasher().Hash(OldPassword),
            ResetTime.AddHours(-1));
        var recoveryToken = VerificationToken.Create(
            Guid.CreateVersion7(ResetTime.AddHours(-1).AddTicks(2)),
            tenantId,
            accountId,
            RecoveryPurpose,
            HashToken(RecoveryRawToken),
            ResetTime.AddHours(1));

        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(account);
        dbContext.Credentials.Add(credential);
        dbContext.VerificationTokens.Add(recoveryToken);
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        return accountId;
    }

    private static AuthenticateStaffSession CreateLoginUseCase(
        IdentityDbContext dbContext,
        IIdentitySessionStore sessionStore)
        => new(
            sessionStore,
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            Fingerprinter(),
            Options.Create(new StaffSessionOptions { InactivityTimeoutMinutes = 30 }),
            new FixedTimeProvider(LoginTime),
            new AuthenticateStaffSessionInputValidator());

    private static ResetStaffPassword CreateResetUseCase(IdentityDbContext dbContext)
        => CreateResetUseCase(dbContext, new IdentityStaffAccountStore(dbContext), ResetTime);

    private static ResetStaffPassword CreateResetUseCase(
        IdentityDbContext dbContext,
        IIdentityStaffAccountStore staffAccountStore,
        DateTimeOffset now)
        => new(
            new IdentityPasswordRecoveryStore(dbContext),
            staffAccountStore,
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            Fingerprinter(),
            new FixedTimeProvider(now),
            new ResetStaffPasswordInputValidator());

    private static IIdempotencyFingerprinter Fingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(
                Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
