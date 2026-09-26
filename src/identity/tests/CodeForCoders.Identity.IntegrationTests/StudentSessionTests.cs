using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StudentSessionTests(IdentityIntegrationFixture fixture)
{
    [Fact]
    public async Task StudentSession_InternalActorIsRejectedWithInvalidCredentials()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var now = TimeProvider.System.GetUtcNow();
        var accountId = Guid.CreateVersion7(now);
        await using (var seedContext = fixture.CreateDbContext(tenantId))
        {
            seedContext.Accounts.Add(Account.CreateInternal(
                accountId,
                tenantId,
                "Internal Actor",
                "internal@example.com",
                "internal@example.com"));
            seedContext.Credentials.Add(Credential.Create(
                Guid.CreateVersion7(now.AddTicks(1)),
                tenantId,
                accountId,
                new Pbkdf2PasswordHasher().Hash("SenhaForte1!"),
                now));
            await new IdentityUnitOfWork(seedContext).CommitAsync(cancellationToken);
        }

        await using var dbContext = fixture.CreateDbContext(tenantId);
        var exception = await Assert.ThrowsAsync<StudentSessionException>(() => CreateLoginUseCase(dbContext).ExecuteAsync(
            new AuthenticateStudentSessionInput(
                tenantId,
                "internal@example.com",
                "SenhaForte1!",
                "student-login-internal"),
            cancellationToken));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", exception.Code);
        Assert.Empty(await dbContext.StudentSessions.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task StudentSession_LoginCreatesOneSessionAndReplaysTheSameLogicalSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var accountId = await SeedStudentAsync(tenantId, true, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateLoginUseCase(dbContext);
        var input = new AuthenticateStudentSessionInput(
            tenantId,
            "student@example.com",
            "SenhaForte1!",
            "login-replay");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);
        var sessions = await dbContext.StudentSessions.ToListAsync(cancellationToken);

        Assert.Equal(accountId, first.AccountId);
        Assert.Equal(first.SessionId, replay.SessionId);
        Assert.Equal(first.AccountId, replay.AccountId);
        Assert.Single(sessions);
        Assert.Equal(first.SessionId, sessions[0].Id);
    }

    [Fact]
    public async Task StudentSession_UnknownEmailAndWrongPasswordHaveIndistinguishableErrors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedStudentAsync(tenantId, true, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateLoginUseCase(dbContext);

        var unknown = await Assert.ThrowsAsync<StudentSessionException>(() => useCase.ExecuteAsync(
            new AuthenticateStudentSessionInput(tenantId, "missing@example.com", "SenhaForte1!", "missing-user"),
            cancellationToken));
        var wrongPassword = await Assert.ThrowsAsync<StudentSessionException>(() => useCase.ExecuteAsync(
            new AuthenticateStudentSessionInput(tenantId, "student@example.com", "SenhaErrada1!", "wrong-password"),
            cancellationToken));

        Assert.Equal(401, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, wrongPassword.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", unknown.Code);
        Assert.Equal(unknown.Code, wrongPassword.Code);
        Assert.Equal(unknown.Title, wrongPassword.Title);
    }

    [Fact]
    public async Task StudentSession_UnconfirmedAccountIsDistinguishedOnlyAfterCorrectPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedStudentAsync(tenantId, false, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateLoginUseCase(dbContext);

        var wrongPassword = await Assert.ThrowsAsync<StudentSessionException>(() => useCase.ExecuteAsync(
            new AuthenticateStudentSessionInput(tenantId, "student@example.com", "SenhaErrada1!", "unconfirmed-wrong"),
            cancellationToken));
        var correctPassword = await Assert.ThrowsAsync<StudentSessionException>(() => useCase.ExecuteAsync(
            new AuthenticateStudentSessionInput(tenantId, "student@example.com", "SenhaForte1!", "unconfirmed-correct"),
            cancellationToken));

        Assert.Equal(401, wrongPassword.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", wrongPassword.Code);
        Assert.Equal(422, correctPassword.StatusCode);
        Assert.Equal("EMAIL_NOT_CONFIRMED", correctPassword.Code);
        Assert.Empty(await dbContext.StudentSessions.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task StudentSession_ActivityRenewsAndLogoutRevokesOnlyTheCurrentSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedStudentAsync(tenantId, true, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var login = CreateLoginUseCase(dbContext);
        var first = await login.ExecuteAsync(
            new AuthenticateStudentSessionInput(tenantId, "student@example.com", "SenhaForte1!", "session-one"),
            cancellationToken);
        var second = await login.ExecuteAsync(
            new AuthenticateStudentSessionInput(tenantId, "student@example.com", "SenhaForte1!", "session-two"),
            cancellationToken);
        var validate = CreateValidateUseCase(dbContext);
        var renewed = await validate.ExecuteAsync(
            new ValidateStudentSessionInput(tenantId, second.SessionId),
            cancellationToken);
        await CreateRevokeUseCase(dbContext).ExecuteAsync(
            new RevokeStudentSessionInput(tenantId, first.SessionId, "logout-one"),
            cancellationToken);

        var firstAfterLogout = await validate.ExecuteAsync(
            new ValidateStudentSessionInput(tenantId, first.SessionId),
            cancellationToken);
        var secondAfterLogout = await validate.ExecuteAsync(
            new ValidateStudentSessionInput(tenantId, second.SessionId),
            cancellationToken);
        var replay = await Assert.ThrowsAsync<StudentSessionException>(() => login.ExecuteAsync(
            new AuthenticateStudentSessionInput(tenantId, "student@example.com", "SenhaForte1!", "session-one"),
            cancellationToken));

        Assert.NotNull(renewed);
        Assert.True(renewed.ExpiresAt >= second.ExpiresAt);
        Assert.Null(firstAfterLogout);
        Assert.NotNull(secondAfterLogout);
        Assert.Equal(401, replay.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", replay.Code);
    }

    private async Task<Guid> SeedStudentAsync(Guid tenantId, bool confirmed, CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();
        var accountId = Guid.CreateVersion7(now);
        var account = Account.CreateStudent(
            accountId,
            tenantId,
            "Ana Souza",
            "student@example.com",
            "student@example.com");
        if (confirmed)
        {
            account.Confirm();
        }

        var credential = Credential.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            tenantId,
            accountId,
            new Pbkdf2PasswordHasher().Hash("SenhaForte1!"),
            now);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(account);
        dbContext.Credentials.Add(credential);
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        return accountId;
    }

    private static AuthenticateStudentSession CreateLoginUseCase(IdentityDbContext dbContext)
        => new(
            new IdentitySessionStore(dbContext),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            CreateFingerprinter(),
            Options.Create(new StudentSessionOptions { InactivityTimeoutMinutes = 30 }),
            TimeProvider.System,
            new AuthenticateStudentSessionInputValidator());

    private static ValidateStudentSession CreateValidateUseCase(IdentityDbContext dbContext)
        => new(
            new IdentitySessionStore(dbContext),
            Options.Create(new StudentSessionOptions { InactivityTimeoutMinutes = 30 }),
            TimeProvider.System,
            new ValidateStudentSessionInputValidator());

    private static RevokeStudentSession CreateRevokeUseCase(IdentityDbContext dbContext)
        => new(
            new IdentitySessionStore(dbContext),
            new IdentityUnitOfWork(dbContext),
            CreateFingerprinter(),
            TimeProvider.System,
            new RevokeStudentSessionInputValidator());

    private static IIdempotencyFingerprinter CreateFingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(
                Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));
}
