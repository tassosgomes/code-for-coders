using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStudentPassword;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StudentPasswordChangeTests(IdentityIntegrationFixture fixture)
{
    [Fact]
    public async Task StudentPasswordChange_ReplacesLegacyCredentialConsumesRecoveryTokensRevokesOtherSessionsAndWritesOneFact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        Guid accountId;
        Guid currentSessionId;
        Guid otherSessionId;
        await using (var setupContext = fixture.CreateDbContext(tenantId))
        {
            (accountId, currentSessionId, otherSessionId) = await SeedStudentAsync(
                setupContext,
                tenantId,
                "legacy",
                cancellationToken);
        }

        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateUseCase(dbContext);
        var input = new ChangeStudentPasswordInput(
            tenantId,
            currentSessionId,
            "legacy",
            "NovaSenha2!",
            "password-change-success-1");
        var result = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);
        var conflict = await Assert.ThrowsAsync<StudentPasswordChangeException>(() => useCase.ExecuteAsync(
            input with { NewPassword = "OutraSenha3!" },
            cancellationToken));

        Assert.Equal(204, result.StatusCode);
        Assert.Equal(result, replay);
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflict.Code);

        await using var verificationContext = fixture.CreateDbContext(tenantId);
        var credential = await verificationContext.Credentials.SingleAsync(cancellationToken);
        var sessions = await verificationContext.StudentSessions.OrderBy(session => session.Id).ToListAsync(cancellationToken);
        var tokens = await verificationContext.VerificationTokens.ToListAsync(cancellationToken);
        var fact = await verificationContext.OutboxMessages.SingleAsync(
            message => message.RoutingKey == "identidade.senha-redefinida.v1",
            cancellationToken);

        Assert.True(new Pbkdf2PasswordHasher().Verify("NovaSenha2!", credential.PasswordHash));
        Assert.False(new Pbkdf2PasswordHasher().Verify("legacy", credential.PasswordHash));
        Assert.Equal(2, sessions.Count);
        Assert.Null(sessions.Single(session => session.Id == currentSessionId).RevokedOn);
        Assert.NotNull(sessions.Single(session => session.Id == otherSessionId).RevokedOn);
        Assert.All(tokens, token => Assert.NotNull(token.ConsumedOn));
        Assert.DoesNotContain("legacy", fact.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("NovaSenha2!", fact.Payload, StringComparison.Ordinal);
        Assert.Equal(tenantId, fact.TenantId);
        using var factPayload = JsonDocument.Parse(fact.Payload);
        Assert.Equal(accountId, factPayload.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal(1, await verificationContext.OutboxMessages.CountAsync(
            message => message.RoutingKey == "identidade.senha-redefinida.v1",
            cancellationToken));

        await using var authenticationContext = fixture.CreateDbContext(tenantId);
        var authenticator = new AuthenticateStudentSession(
            new IdentitySessionStore(authenticationContext),
            new IdentityUnitOfWork(authenticationContext),
            new Pbkdf2PasswordHasher(),
            CreateFingerprinter(),
            Options.Create(new StudentSessionOptions { InactivityTimeoutMinutes = 30 }),
            TimeProvider.System,
            new AuthenticateStudentSessionInputValidator());
        var authenticated = await authenticator.ExecuteAsync(
            new AuthenticateStudentSessionInput(
                tenantId,
                "student@example.com",
                "NovaSenha2!",
                "password-change-login-new-1"),
            cancellationToken);
        var oldPassword = await Assert.ThrowsAsync<StudentSessionException>(() => authenticator.ExecuteAsync(
            new AuthenticateStudentSessionInput(
                tenantId,
                "student@example.com",
                "legacy",
                "password-change-login-old-1"),
            cancellationToken));
        Assert.Equal(accountId, authenticated.AccountId);
        Assert.Equal(401, oldPassword.StatusCode);
    }

    [Fact]
    public async Task StudentPasswordChange_RejectsIncorrectCurrentPasswordWithoutChangingCredentialsTokensSessionsOrFact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var (accountId, currentSessionId, _) = await SeedStudentAsync(
            dbContext,
            tenantId,
            "SenhaForte1!",
            cancellationToken);
        var originalHash = (await dbContext.Credentials.SingleAsync(cancellationToken)).PasswordHash;
        var useCase = CreateUseCase(dbContext);
        var input = new ChangeStudentPasswordInput(
            tenantId,
            currentSessionId,
            "SenhaErrada1!",
            "NovaSenha2!",
            "password-change-wrong-current-2");

        var rejected = await Assert.ThrowsAsync<StudentPasswordChangeException>(() => useCase.ExecuteAsync(input, cancellationToken));
        var replay = await Assert.ThrowsAsync<StudentPasswordChangeException>(() => useCase.ExecuteAsync(input, cancellationToken));

        Assert.Equal("PASSWORD_CHANGE_REJECTED", rejected.Code);
        Assert.Equal(rejected.Code, replay.Code);
        await using var verificationContext = fixture.CreateDbContext(tenantId);
        Assert.Equal(originalHash, (await verificationContext.Credentials.SingleAsync(cancellationToken)).PasswordHash);
        Assert.All(await verificationContext.VerificationTokens.ToListAsync(cancellationToken), token => Assert.Null(token.ConsumedOn));
        Assert.All(await verificationContext.StudentSessions.ToListAsync(cancellationToken), session => Assert.Null(session.RevokedOn));
        Assert.Empty(await verificationContext.OutboxMessages.Where(
            message => message.RoutingKey == "identidade.senha-redefinida.v1").ToListAsync(cancellationToken));
        Assert.Equal(accountId, (await verificationContext.Accounts.SingleAsync(cancellationToken)).Id);
    }

    [Fact]
    public async Task StudentPasswordChange_RejectsAWeakNewPasswordWithoutChangingCredentialsTokensSessionsOrFact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var (_, currentSessionId, _) = await SeedStudentAsync(
            dbContext,
            tenantId,
            "legacy",
            cancellationToken);
        var originalHash = (await dbContext.Credentials.SingleAsync(cancellationToken)).PasswordHash;
        var useCase = CreateUseCase(dbContext);

        var rejected = await Assert.ThrowsAsync<StudentPasswordChangeException>(() => useCase.ExecuteAsync(
            new ChangeStudentPasswordInput(
                tenantId,
                currentSessionId,
                "legacy",
                "fraca",
                "password-change-weak-new-3"),
            cancellationToken));

        Assert.Equal("PASSWORD_CHANGE_REJECTED", rejected.Code);
        await using var verificationContext = fixture.CreateDbContext(tenantId);
        Assert.Equal(originalHash, (await verificationContext.Credentials.SingleAsync(cancellationToken)).PasswordHash);
        Assert.All(await verificationContext.VerificationTokens.ToListAsync(cancellationToken), token => Assert.Null(token.ConsumedOn));
        Assert.All(await verificationContext.StudentSessions.ToListAsync(cancellationToken), session => Assert.Null(session.RevokedOn));
        Assert.Empty(await verificationContext.OutboxMessages.Where(
            message => message.RoutingKey == "identidade.senha-redefinida.v1").ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task StudentPasswordChange_RejectsTheOtherSessionOnItsNextValidationAndKeepsTheCurrentSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        Guid currentSessionId;
        Guid otherSessionId;
        await using (var setupContext = fixture.CreateDbContext(tenantId))
        {
            (_, currentSessionId, otherSessionId) = await SeedStudentAsync(
                setupContext,
                tenantId,
                "SenhaForte1!",
                cancellationToken);
        }

        await using (var changeContext = fixture.CreateDbContext(tenantId))
        {
            var output = await CreateUseCase(changeContext).ExecuteAsync(
                new ChangeStudentPasswordInput(
                    tenantId,
                    currentSessionId,
                    "SenhaForte1!",
                    "NovaSenha2!",
                    "password-change-session-revoke-4"),
                cancellationToken);
            Assert.Equal(204, output.StatusCode);
        }

        await using var validationContext = fixture.CreateDbContext(tenantId);
        var validator = new ValidateStudentSession(
            new IdentitySessionStore(validationContext),
            Options.Create(new StudentSessionOptions { InactivityTimeoutMinutes = 30 }),
            TimeProvider.System,
            new ValidateStudentSessionInputValidator());
        var current = await validator.ExecuteAsync(new ValidateStudentSessionInput(tenantId, currentSessionId), cancellationToken);
        var other = await validator.ExecuteAsync(new ValidateStudentSessionInput(tenantId, otherSessionId), cancellationToken);

        Assert.NotNull(current);
        Assert.Null(other);
    }

    private static async Task<(Guid AccountId, Guid CurrentSessionId, Guid OtherSessionId)> SeedStudentAsync(
        IdentityDbContext dbContext,
        Guid tenantId,
        string currentPassword,
        CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();
        var accountId = Guid.CreateVersion7(now);
        var account = Account.CreateStudent(
            accountId,
            tenantId,
            "Ana Souza",
            "student@example.com",
            "student@example.com");
        account.Confirm();
        dbContext.Accounts.Add(account);
        dbContext.Credentials.Add(Credential.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            tenantId,
            accountId,
            new Pbkdf2PasswordHasher().Hash(currentPassword),
            now));

        var currentSessionId = Guid.CreateVersion7(now.AddTicks(2));
        var otherSessionId = Guid.CreateVersion7(now.AddTicks(3));
        dbContext.StudentSessions.AddRange(
            StudentSession.Create(currentSessionId, tenantId, accountId, now, now.AddHours(1)),
            StudentSession.Create(otherSessionId, tenantId, accountId, now, now.AddHours(1)));

        dbContext.VerificationTokens.AddRange(
            VerificationToken.Create(
                Guid.CreateVersion7(now.AddTicks(4)),
                tenantId,
                accountId,
                "recuperacao-de-senha",
                HashToken("pending-reset-one"),
                now.AddHours(1)),
            VerificationToken.Create(
                Guid.CreateVersion7(now.AddTicks(5)),
                tenantId,
                accountId,
                "recuperacao-de-senha",
                HashToken("pending-reset-two"),
                now.AddHours(1)));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        return (accountId, currentSessionId, otherSessionId);
    }

    private static ChangeStudentPassword CreateUseCase(IdentityDbContext dbContext)
    {
        var destinations = Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            NotificationExchange = "notification.events.default",
        });
        var registration = Options.Create(new RegistrationOptions
        {
            ConfirmationBaseUrl = "https://students.example.test/confirm-account",
            ConfirmationLifetimeHours = 24,
            PasswordResetBaseUrl = "https://students.example.test/redefinir-senha",
            PasswordResetLifetimeHours = 1,
        });
        return new ChangeStudentPassword(
            new IdentityPasswordRecoveryStore(dbContext),
            new StudentPasswordRecoveryMessageWriter(
                new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector),
                registration,
                destinations),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            CreateFingerprinter(),
            TimeProvider.System,
            new ChangeStudentPasswordInputValidator());
    }

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static IIdempotencyFingerprinter CreateFingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));
}
