using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StaffSessionTests(IdentityIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(StaffSession_LoginCreatesSessionWithEffectivePermissionsAndReplaysIt))]
    [Trait("Layer", "Identity staff session - Integration")]
    public async Task StaffSession_LoginCreatesSessionWithEffectivePermissionsAndReplaysIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var accountId = await SeedInternalAsync(tenantId, "admin@example.com", StaffRoleCatalog.Administrator, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateLoginUseCase(dbContext);
        var input = new AuthenticateStaffSessionInput(tenantId, "admin@example.com", "SenhaForte1!", "staff-login-replay");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);

        Assert.Equal(accountId, first.AccountId);
        Assert.Equal(first.SessionId, replay.SessionId);
        Assert.Equal([StaffRoleCatalog.Administrator], first.Roles);
        Assert.Equal([StaffRoleCatalog.ManageAccess], first.Permissions);
        Assert.Single(await dbContext.StaffSessions.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffSession_UnknownEmailWrongPasswordAndStudentAccountHaveSameUnauthorizedError))]
    [Trait("Layer", "Identity staff session - Integration")]
    public async Task StaffSession_UnknownEmailWrongPasswordAndStudentAccountHaveSameUnauthorizedError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInternalAsync(tenantId, "admin@example.com", null, cancellationToken);
        await SeedStudentAsync(tenantId, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateLoginUseCase(dbContext);

        var wrongPassword = await AssertUnauthorizedAsync(useCase, tenantId, "admin@example.com", "bad", "wrong-password", cancellationToken);
        var unknownEmail = await AssertUnauthorizedAsync(useCase, tenantId, "missing@example.com", "SenhaForte1!", "unknown-email", cancellationToken);
        var studentAccount = await AssertUnauthorizedAsync(useCase, tenantId, "student@example.com", "SenhaForte1!", "student-account", cancellationToken);

        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, studentAccount.StatusCode);
        Assert.Equal(wrongPassword.Code, unknownEmail.Code);
        Assert.Equal(wrongPassword.Code, studentAccount.Code);
        Assert.Equal(wrongPassword.Title, unknownEmail.Title);
        Assert.Equal(wrongPassword.Title, studentAccount.Title);
        Assert.Equal("INVALID_CREDENTIALS", wrongPassword.Code);
        Assert.Empty(await dbContext.StaffSessions.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffSession_InternalAccountWithoutRoleCanEnterWithoutPermissions))]
    [Trait("Layer", "Identity staff session - Integration")]
    public async Task StaffSession_InternalAccountWithoutRoleCanEnterWithoutPermissions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var accountId = await SeedInternalAsync(tenantId, "operator@example.com", null, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var session = await CreateLoginUseCase(dbContext).ExecuteAsync(
            new AuthenticateStaffSessionInput(tenantId, "operator@example.com", "SenhaForte1!", "staff-no-role"),
            cancellationToken);

        Assert.Equal(accountId, session.AccountId);
        Assert.Empty(session.Roles);
        Assert.Empty(session.Permissions);
    }

    [Fact(DisplayName = nameof(StaffSession_ValidationRenewsTheSessionAndReturnsCurrentPermissions))]
    [Trait("Layer", "Identity staff session - Integration")]
    public async Task StaffSession_ValidationRenewsTheSessionAndReturnsCurrentPermissions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInternalAsync(tenantId, "admin@example.com", StaffRoleCatalog.Administrator, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var login = await CreateLoginUseCase(dbContext).ExecuteAsync(
            new AuthenticateStaffSessionInput(tenantId, "admin@example.com", "SenhaForte1!", "staff-validate-login"),
            cancellationToken);

        var validation = await CreateValidateUseCase(dbContext).ExecuteAsync(
            new ValidateStaffSessionInput(tenantId, login.SessionId),
            cancellationToken);

        Assert.NotNull(validation);
        Assert.Equal(login.AccountId, validation.Session.AccountId);
        Assert.Equal([StaffRoleCatalog.Administrator], validation.Session.Roles);
        Assert.Equal([StaffRoleCatalog.ManageAccess], validation.Session.Permissions);
        Assert.True(validation.Session.ExpiresAt >= login.ExpiresAt);
    }

    [Fact(DisplayName = nameof(StaffSession_LogoutRevokesOnlyTheRequestedSessionAndReplayDoesNotReactivateIt))]
    [Trait("Layer", "Identity staff session - Integration")]
    public async Task StaffSession_LogoutRevokesOnlyTheRequestedSessionAndReplayDoesNotReactivateIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await SeedInternalAsync(tenantId, "admin@example.com", StaffRoleCatalog.Administrator, cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var login = CreateLoginUseCase(dbContext);
        var first = await login.ExecuteAsync(
            new AuthenticateStaffSessionInput(tenantId, "admin@example.com", "SenhaForte1!", "staff-session-one"),
            cancellationToken);
        var second = await login.ExecuteAsync(
            new AuthenticateStaffSessionInput(tenantId, "admin@example.com", "SenhaForte1!", "staff-session-two"),
            cancellationToken);
        var revoke = CreateRevokeUseCase(dbContext);

        Assert.True(await revoke.ExecuteAsync(new RevokeStaffSessionInput(tenantId, first.SessionId, "staff-logout-one"), cancellationToken));
        Assert.True(await revoke.ExecuteAsync(new RevokeStaffSessionInput(tenantId, first.SessionId, "staff-logout-one"), cancellationToken));
        var firstValidation = await CreateValidateUseCase(dbContext).ExecuteAsync(
            new ValidateStaffSessionInput(tenantId, first.SessionId), cancellationToken);
        var secondValidation = await CreateValidateUseCase(dbContext).ExecuteAsync(
            new ValidateStaffSessionInput(tenantId, second.SessionId), cancellationToken);

        Assert.Null(firstValidation);
        Assert.NotNull(secondValidation);
    }

    private async Task<Guid> SeedInternalAsync(
        Guid tenantId,
        string email,
        string? role,
        CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();
        var accountId = Guid.CreateVersion7(now);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(Account.CreateInternal(accountId, tenantId, "Internal Actor", email, email.ToLowerInvariant()));
        dbContext.Credentials.Add(Credential.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            tenantId,
            accountId,
            new Pbkdf2PasswordHasher().Hash("SenhaForte1!"),
            now));
        if (role is not null)
        {
            dbContext.StaffRoleAssignments.Add(StaffRoleAssignment.Create(
                Guid.CreateVersion7(now.AddTicks(2)), tenantId, accountId, role, now));
        }

        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        return accountId;
    }

    private async Task SeedStudentAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();
        var accountId = Guid.CreateVersion7(now);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(Account.CreateStudent(
            accountId,
            tenantId,
            "Student",
            "student@example.com",
            "student@example.com"));
        dbContext.Credentials.Add(Credential.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            tenantId,
            accountId,
            new Pbkdf2PasswordHasher().Hash("SenhaForte1!"),
            now));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
    }

    private static async Task<StaffSessionException> AssertUnauthorizedAsync(
        AuthenticateStaffSession useCase,
        Guid tenantId,
        string email,
        string password,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var exception = await Assert.ThrowsAsync<StaffSessionException>(() => useCase.ExecuteAsync(
            new AuthenticateStaffSessionInput(tenantId, email, password, idempotencyKey),
            cancellationToken));
        Assert.Equal(401, exception.StatusCode);
        return exception;
    }

    private static AuthenticateStaffSession CreateLoginUseCase(IdentityDbContext dbContext)
        => new(
            new IdentitySessionStore(dbContext),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            CreateFingerprinter(),
            Options.Create(new StaffSessionOptions { InactivityTimeoutMinutes = 30 }),
            TimeProvider.System,
            new AuthenticateStaffSessionInputValidator());

    private static ValidateStaffSession CreateValidateUseCase(IdentityDbContext dbContext)
        => new(
            new IdentitySessionStore(dbContext),
            Options.Create(new StaffSessionOptions { InactivityTimeoutMinutes = 30 }),
            TimeProvider.System,
            new ValidateStaffSessionInputValidator());

    private static RevokeStaffSession CreateRevokeUseCase(IdentityDbContext dbContext)
        => new(
            new IdentitySessionStore(dbContext),
            new IdentityUnitOfWork(dbContext),
            CreateFingerprinter(),
            TimeProvider.System,
            new RevokeStaffSessionInputValidator());

    private static IIdempotencyFingerprinter CreateFingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(
                Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));
}
