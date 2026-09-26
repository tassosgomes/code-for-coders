using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.ProvisionFirstAdministrator;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStaffPassword;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StaffPasswordResetTests(IdentityIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(ResetStaffPassword_CreatesFirstCredentialAndRevokesSessionsInOneCommit))]
    [Trait("Layer", "Identity staff password reset - Integration")]
    public async Task ResetStaffPassword_CreatesFirstCredentialAndRevokesSessionsInOneCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var rawToken = await ProvisionAndReadTokenAsync(dbContext, tenantId, cancellationToken);
        var account = await dbContext.Accounts.SingleAsync(cancellationToken);
        var now = TimeProvider.System.GetUtcNow();
        dbContext.VerificationTokens.Add(VerificationToken.Create(
            Guid.CreateVersion7(now),
            tenantId,
            account.Id,
            "recuperacao-de-senha",
            HashToken("second-reset-secret"),
            now.AddHours(1)));
        dbContext.StaffSessions.AddRange(
            StaffSession.Create(Guid.CreateVersion7(now), tenantId, account.Id, now, now.AddHours(1)),
            StaffSession.Create(Guid.CreateVersion7(now.AddTicks(1)), tenantId, account.Id, now, now.AddHours(1)));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var useCase = CreateResetUseCase(dbContext);
        var input = new ResetStaffPasswordInput(tenantId, rawToken, "SenhaForte1!", "staff-reset-valid-1");
        var result = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);

        Assert.Equal(204, result.StatusCode);
        Assert.Equal(result, replay);
        await using var verifyContext = fixture.CreateDbContext(tenantId);
        var credential = await verifyContext.Credentials.SingleAsync(cancellationToken);
        var sessions = await verifyContext.StaffSessions.ToListAsync(cancellationToken);
        var tokens = await verifyContext.VerificationTokens
            .Where(token => token.Purpose == "recuperacao-de-senha")
            .ToListAsync(cancellationToken);
        var hasher = new Pbkdf2PasswordHasher();
        Assert.True(hasher.Verify("SenhaForte1!", credential.PasswordHash));
        Assert.All(sessions, session => Assert.NotNull(session.RevokedOn));
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, token => Assert.NotNull(token.ConsumedOn));
        Assert.Empty(await verifyContext.OutboxMessages
            .Where(message => message.RoutingKey == "identidade.senha-redefinida.v1"
                || message.RoutingKey == "auditoria.ato-praticado.v1")
            .ToListAsync(cancellationToken));
        Assert.Single(await verifyContext.OutboxMessages
            .Where(message => message.RoutingKey == "notificacao.envio-solicitado.v1")
            .ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(ResetStaffPassword_RejectsAStudentRecoveryToken))]
    [Trait("Layer", "Identity staff password reset - Integration")]
    public async Task ResetStaffPassword_RejectsAStudentRecoveryToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var account = Account.CreateStudent(
            Guid.CreateVersion7(),
            tenantId,
            "Student",
            "student@example.com",
            "student@example.com");
        dbContext.Accounts.Add(account);
        dbContext.VerificationTokens.Add(VerificationToken.Create(
            Guid.CreateVersion7(),
            tenantId,
            account.Id,
            "recuperacao-de-senha",
            HashToken("student-reset-secret"),
            TimeProvider.System.GetUtcNow().AddHours(1)));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var exception = await Assert.ThrowsAsync<StaffPasswordRecoveryException>(() =>
            CreateResetUseCase(dbContext).ExecuteAsync(
                new ResetStaffPasswordInput(tenantId, "student-reset-secret", "SenhaForte1!", "staff-reset-student-1"),
                cancellationToken));

        Assert.Equal("RESET_TOKEN_INVALID", exception.Code);
        await using var verifyContext = fixture.CreateDbContext(tenantId);
        Assert.Empty(await verifyContext.Credentials.ToListAsync(cancellationToken));
        Assert.Null((await verifyContext.VerificationTokens.SingleAsync(cancellationToken)).ConsumedOn);
    }

    [Fact(DisplayName = nameof(ResetStaffPassword_RejectsWeakPasswordWithoutConsumingResetToken))]
    [Trait("Layer", "Identity staff password reset - Integration")]
    public async Task ResetStaffPassword_RejectsWeakPasswordWithoutConsumingResetToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var rawToken = await ProvisionAndReadTokenAsync(dbContext, tenantId, cancellationToken);

        var exception = await Assert.ThrowsAsync<StaffPasswordRecoveryException>(() =>
            CreateResetUseCase(dbContext).ExecuteAsync(
                new ResetStaffPasswordInput(tenantId, rawToken, "weak", "staff-reset-weak-1"),
                cancellationToken));

        Assert.Equal("PASSWORD_POLICY_VIOLATION", exception.Code);
        await using var verifyContext = fixture.CreateDbContext(tenantId);
        Assert.Empty(await verifyContext.Credentials.ToListAsync(cancellationToken));
        Assert.Null((await verifyContext.VerificationTokens.SingleAsync(cancellationToken)).ConsumedOn);

        var accepted = await CreateResetUseCase(dbContext).ExecuteAsync(
            new ResetStaffPasswordInput(tenantId, rawToken, "SenhaForte1!", "staff-reset-weak-2"),
            cancellationToken);
        Assert.Equal(204, accepted.StatusCode);
    }

    [Fact(DisplayName = nameof(ResetStaffPassword_RejectsAnAlreadyUsedLinkWithAnotherIdempotencyKey))]
    [Trait("Layer", "Identity staff password reset - Integration")]
    public async Task ResetStaffPassword_RejectsAnAlreadyUsedLinkWithAnotherIdempotencyKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var rawToken = await ProvisionAndReadTokenAsync(dbContext, tenantId, cancellationToken);
        var accepted = await CreateResetUseCase(dbContext).ExecuteAsync(
            new ResetStaffPasswordInput(tenantId, rawToken, "SenhaForte1!", "staff-reset-reuse-1"),
            cancellationToken);
        Assert.Equal(204, accepted.StatusCode);

        var exception = await Assert.ThrowsAsync<StaffPasswordRecoveryException>(() =>
            CreateResetUseCase(dbContext).ExecuteAsync(
                new ResetStaffPasswordInput(tenantId, rawToken, "OutraSenha2@", "staff-reset-reuse-2"),
                cancellationToken));

        Assert.Equal("RESET_TOKEN_INVALID", exception.Code);
        await using var verifyContext = fixture.CreateDbContext(tenantId);
        var credential = await verifyContext.Credentials.SingleAsync(cancellationToken);
        var hasher = new Pbkdf2PasswordHasher();
        Assert.True(hasher.Verify("SenhaForte1!", credential.PasswordHash));
        Assert.False(hasher.Verify("OutraSenha2@", credential.PasswordHash));
    }

    private static async Task<string> ProvisionAndReadTokenAsync(
        IdentityDbContext dbContext,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var destinations = Destinations();
        var options = StaffAccountOptions();
        var writer = new StaffPasswordRecoveryMessageWriter(
            new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector),
            options,
            destinations);
        var useCase = new ProvisionFirstAdministrator(
            new IdentityStaffAccountStore(dbContext),
            new IdentityPasswordRecoveryStore(dbContext),
            writer,
            new IdentityUnitOfWork(dbContext),
            options,
            TimeProvider.System,
            new ProvisionFirstAdministratorInputValidator());
        var result = await useCase.ExecuteAsync(
            new ProvisionFirstAdministratorInput(tenantId, "admin@example.com", "First Admin"),
            cancellationToken);
        Assert.Equal(ProvisionFirstAdministratorStatus.Created, result.Status);

        var message = await dbContext.OutboxMessages.SingleAsync(cancellationToken);
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(message));
        return ReadToken(payload.RootElement.GetProperty("dados").GetProperty("link").GetString()!);
    }

    private static ResetStaffPassword CreateResetUseCase(IdentityDbContext dbContext)
        => new(
            new IdentityPasswordRecoveryStore(dbContext),
            new IdentityStaffAccountStore(dbContext),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            CreateFingerprinter(),
            TimeProvider.System,
            new ResetStaffPasswordInputValidator());

    private static IOptions<StaffAccountOptions> StaffAccountOptions()
        => Options.Create(new StaffAccountOptions
        {
            PasswordResetBaseUrl = "https://staff.example.test/admin/redefinir-senha",
            PasswordResetLifetimeHours = 1,
        });

    private static IOptions<OutboxDestinationOptions> Destinations()
        => Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            NotificationExchange = "notification.events.default",
        });

    private static IIdempotencyFingerprinter CreateFingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(
                Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));

    private static string ReadToken(string link)
        => Uri.UnescapeDataString(new Uri(link).Query.TrimStart('?').Split("token=", 2, StringSplitOptions.None)[1]);

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
