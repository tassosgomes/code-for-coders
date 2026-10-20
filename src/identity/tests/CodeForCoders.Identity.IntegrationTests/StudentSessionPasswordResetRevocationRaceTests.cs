using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStudentPassword;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;
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
/// RACE #8 - "A concurrent login may escape the session revocation of a password reset".
///
/// Two isolated DbContext scopes. Scope A (login) authenticates the student, verifies the password
/// hash and then parks on a barrier placed right before the new session row is staged. While A is
/// parked, scope B runs a password reset: it rewrites the credential and revokes every session it
/// can see - but A's session does not exist yet, so it would not be revoked. A is released.
///
/// Regression guard for the fix (xmin optimistic concurrency token on Credential + a transactional
/// "touch" of the authenticated credential inside AuthenticateStudentSession): the reset bumps the
/// credential's row version, so the login's commit - which now updates that credential under the
/// xmin it read - fails with a concurrency conflict. The login re-reads the now-current credential,
/// the password it authenticated against no longer matches, and it is rejected with
/// INVALID_CREDENTIALS. No session is ever persisted, even with the deterministic barrier holding
/// the window open.
/// </summary>
[Collection(IdentityIntegrationCollection.Name)]
public sealed class StudentSessionPasswordResetRevocationRaceTests(IdentityIntegrationFixture fixture)
{
    private static readonly DateTimeOffset ResetTime = new(2026, 10, 3, 9, 12, 40, TimeSpan.Zero);
    private static readonly DateTimeOffset LoginTime = ResetTime.AddSeconds(5);
    private const string Email = "race8-student@example.com";
    private const string OldPassword = "SenhaForte1!";
    private const string NewPassword = "NovaSenha2!";
    private const string RecoveryPurpose = "recuperacao-de-senha";
    private const string RecoveryRawToken = "race8_recovery_token_value";

    [Fact(DisplayName = nameof(Race8_LoginConcurrentWithPasswordReset_DoesNotCreateAValidSession))]
    [Trait("Layer", "Identity student session - Integration")]
    public async Task Race8_LoginConcurrentWithPasswordReset_DoesNotCreateAValidSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var accountId = await SeedConfirmedStudentWithRecoveryTokenAsync(tenantId, cancellationToken);

        var barrier = new ConcurrencyBarrier();

        // Scope A: login, parked just before the new session row is staged.
        await using var loginContext = fixture.CreateDbContext(tenantId);
        var loginTask = Task.Run(
            async () => await CreateLoginUseCase(loginContext, barrier).ExecuteAsync(
                new AuthenticateStudentSessionInput(tenantId, Email, OldPassword, "race8-login"),
                cancellationToken),
            cancellationToken);

        await barrier.Reached;

        // Scope B: reset the password (rewrites credential, revokes every visible session) and commit.
        await using (var resetContext = fixture.CreateDbContext(tenantId))
        {
            var reset = await CreateResetUseCase(resetContext).ExecuteAsync(
                new ResetStudentPasswordInput(tenantId, RecoveryRawToken, NewPassword, "race8-reset"),
                cancellationToken);
            Assert.Equal(204, reset.StatusCode);
        }

        barrier.Release();

        StudentSessionException? rejection = null;
        AuthenticateStudentSessionOutput? login = null;
        try
        {
            login = await loginTask;
        }
        catch (StudentSessionException exception)
        {
            rejection = exception;
        }

        await using var verify = fixture.CreateDbContext(tenantId);
        var sessions = await verify.StudentSessions.AsNoTracking().ToListAsync(cancellationToken);
        var credential = await verify.Credentials.AsNoTracking().SingleAsync(cancellationToken);

        TestContext.Current.SendDiagnosticMessage(
            "RACE8 outcome: loginSucceeded={0} rejectedCode={1} sessionCount={2} "
            + "credentialMatchesNewPassword={3}",
            login is not null,
            rejection?.Code ?? "<none>",
            sessions.Count,
            new Pbkdf2PasswordHasher().Verify(NewPassword, credential.PasswordHash));

        // Password really was reset: the credential now matches the new password only.
        Assert.True(new Pbkdf2PasswordHasher().Verify(NewPassword, credential.PasswordHash));
        Assert.False(new Pbkdf2PasswordHasher().Verify(OldPassword, credential.PasswordHash));

        // The race is PREVENTED: the concurrent login loses to the reset and is rejected.
        Assert.True(
            login is null,
            "Expected the concurrent login to be rejected, but it minted a session that escaped the "
            + "password reset revocation (race NOT closed).");
        Assert.NotNull(rejection);
        Assert.Equal(401, rejection!.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", rejection.Code);

        // No session row ever materialized for the account.
        Assert.DoesNotContain(sessions, session => session.AccountId == accountId);
    }

    /// <summary>
    /// Reverse interleaving of the RACE #8 guard: the login now re-stages the credential under its
    /// xmin token, so a login that commits while a reset is in flight bumps the row version the reset
    /// read. The reset must still win - re-read and re-apply - and revoke that login's session, instead
    /// of answering the legitimate user with "invalid reset link".
    /// </summary>
    [Fact(DisplayName = nameof(Race8_LoginCommittedDuringPasswordReset_ResetStillWinsAndRevokesIt))]
    [Trait("Layer", "Identity student session - Integration")]
    public async Task Race8_LoginCommittedDuringPasswordReset_ResetStillWinsAndRevokesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedConfirmedStudentWithRecoveryTokenAsync(tenantId, cancellationToken);

        var barrier = new ConcurrencyBarrier();

        // Scope A: reset, parked after it read and rewrote the credential, before its commit.
        await using var resetContext = fixture.CreateDbContext(tenantId);
        var resetTask = Task.Run(
            async () => await CreateResetUseCase(
                    resetContext,
                    new BarrieredPasswordRecoveryStore(new IdentityPasswordRecoveryStore(resetContext), barrier),
                    LoginTime.AddSeconds(5))
                .ExecuteAsync(
                    new ResetStudentPasswordInput(tenantId, RecoveryRawToken, NewPassword, "race8-reverse-reset"),
                    cancellationToken),
            cancellationToken);

        await barrier.Reached;

        // Scope B: a login with the old password commits meanwhile, re-staging the credential.
        Guid loginSessionId;
        await using (var loginContext = fixture.CreateDbContext(tenantId))
        {
            var login = await CreateLoginUseCase(loginContext, new IdentitySessionStore(loginContext)).ExecuteAsync(
                new AuthenticateStudentSessionInput(tenantId, Email, OldPassword, "race8-reverse-login"),
                cancellationToken);
            loginSessionId = login.SessionId;
        }

        barrier.Release();
        var reset = await resetTask;

        await using var verify = fixture.CreateDbContext(tenantId);
        var credential = await verify.Credentials.AsNoTracking().SingleAsync(cancellationToken);
        var loginSession = await verify.StudentSessions.AsNoTracking()
            .SingleAsync(item => item.Id == loginSessionId, cancellationToken);

        Assert.Equal(204, reset.StatusCode);
        Assert.True(new Pbkdf2PasswordHasher().Verify(NewPassword, credential.PasswordHash));
        Assert.NotNull(loginSession.RevokedOn);
    }

    [Fact(DisplayName = nameof(Race8_PasswordResetAfterExistingSession_RevokesItAsExpected))]
    [Trait("Layer", "Identity student session - Integration")]
    public async Task Race8_PasswordResetAfterExistingSession_RevokesItAsExpected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var accountId = await SeedConfirmedStudentWithRecoveryTokenAsync(tenantId, cancellationToken);

        // A session that already exists BEFORE the reset (the control: no interleaving).
        Guid existingSessionId;
        await using (var seedSession = fixture.CreateDbContext(tenantId))
        {
            var session = StudentSession.Create(
                Guid.CreateVersion7(LoginTime),
                tenantId,
                accountId,
                LoginTime,
                LoginTime.AddMinutes(30));
            existingSessionId = session.Id;
            seedSession.StudentSessions.Add(session);
            await new IdentityUnitOfWork(seedSession).CommitAsync(cancellationToken);
        }

        await using (var resetContext = fixture.CreateDbContext(tenantId))
        {
            var reset = await CreateResetUseCase(resetContext).ExecuteAsync(
                new ResetStudentPasswordInput(tenantId, RecoveryRawToken, NewPassword, "race8-control-reset"),
                cancellationToken);
            Assert.Equal(204, reset.StatusCode);
        }

        await using var validateContext = fixture.CreateDbContext(tenantId);
        var validated = await CreateValidateUseCase(validateContext).ExecuteAsync(
            new ValidateStudentSessionInput(tenantId, existingSessionId),
            cancellationToken);

        await using var verify = fixture.CreateDbContext(tenantId);
        var storedSession = await verify.StudentSessions.AsNoTracking()
            .SingleAsync(item => item.Id == existingSessionId, cancellationToken);

        Assert.NotNull(storedSession.RevokedOn);
        Assert.Null(validated);
    }

    private async Task<Guid> SeedConfirmedStudentWithRecoveryTokenAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var accountId = Guid.CreateVersion7(ResetTime.AddHours(-1));
        var account = Account.CreateStudent(accountId, tenantId, "Ana Souza", Email, Email);
        account.Confirm();
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

    private static AuthenticateStudentSession CreateLoginUseCase(IdentityDbContext dbContext, ConcurrencyBarrier barrier)
        => CreateLoginUseCase(dbContext, new BarrieredSessionStore(new IdentitySessionStore(dbContext), barrier));

    private static AuthenticateStudentSession CreateLoginUseCase(
        IdentityDbContext dbContext,
        IIdentitySessionStore sessionStore)
        => new(
            sessionStore,
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            Fingerprinter(),
            Options.Create(new StudentSessionOptions { InactivityTimeoutMinutes = 30 }),
            new FixedTimeProvider(LoginTime),
            new AuthenticateStudentSessionInputValidator());

    private static ValidateStudentSession CreateValidateUseCase(IdentityDbContext dbContext)
        => new(
            new IdentitySessionStore(dbContext),
            Options.Create(new StudentSessionOptions { InactivityTimeoutMinutes = 30 }),
            new FixedTimeProvider(LoginTime.AddMinutes(1)),
            new ValidateStudentSessionInputValidator());

    private static ResetStudentPassword CreateResetUseCase(IdentityDbContext dbContext)
        => CreateResetUseCase(dbContext, new IdentityPasswordRecoveryStore(dbContext), ResetTime);

    private static ResetStudentPassword CreateResetUseCase(
        IdentityDbContext dbContext,
        IIdentityPasswordRecoveryStore recoveryStore,
        DateTimeOffset now)
    {
        var destinations = Destinations();
        return new ResetStudentPassword(
            recoveryStore,
            new StudentPasswordRecoveryMessageWriter(
                new OutboxMessageWriter(dbContext, destinations, Protector()),
                RegistrationOptions(),
                destinations),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            Fingerprinter(),
            new FixedTimeProvider(now),
            new ResetStudentPasswordInputValidator());
    }

    private static IOptions<RegistrationOptions> RegistrationOptions()
        => Options.Create(new RegistrationOptions
        {
            ConfirmationBaseUrl = "https://students.example.test/confirm-account",
            ConfirmationLifetimeHours = 24,
            PasswordResetBaseUrl = "https://students.example.test/redefinir-senha",
            PasswordResetLifetimeHours = 1,
        });

    private static IOptions<OutboxDestinationOptions> Destinations()
        => Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
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
