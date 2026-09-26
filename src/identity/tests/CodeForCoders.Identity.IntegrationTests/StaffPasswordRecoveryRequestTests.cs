using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStaffPasswordReset;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StaffPasswordRecoveryRequestTests(IdentityIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(StaffPasswordRecoveryRequest_ReturnsNeutralResponsesAndEmailsOnlyInternalAccounts))]
    [Trait("Layer", "Identity staff password recovery request - Integration")]
    public async Task StaffPasswordRecoveryRequest_ReturnsNeutralResponsesAndEmailsOnlyInternalAccounts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var now = TimeProvider.System.GetUtcNow();
        var staffAccount = Account.CreateInternal(
            Guid.CreateVersion7(now),
            tenantId,
            "Marina Alves",
            "staff@example.com",
            "staff@example.com");
        var studentAccount = Account.CreateStudent(
            Guid.CreateVersion7(now.AddTicks(1)),
            tenantId,
            "Student",
            "student@example.com",
            "student@example.com");
        dbContext.Accounts.AddRange(staffAccount, studentAccount);
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var useCase = CreateRequestUseCase(dbContext);
        var staffResult = await useCase.ExecuteAsync(
            new RequestStaffPasswordResetInput(tenantId, "STAFF@example.com", "staff-recovery-staff-1"),
            cancellationToken);
        var studentResult = await useCase.ExecuteAsync(
            new RequestStaffPasswordResetInput(tenantId, "student@example.com", "staff-recovery-student-1"),
            cancellationToken);
        var unknownResult = await useCase.ExecuteAsync(
            new RequestStaffPasswordResetInput(tenantId, "unknown@example.com", "staff-recovery-unknown-1"),
            cancellationToken);

        Assert.Equal(new RequestStaffPasswordResetOutput(202), staffResult);
        Assert.Equal(staffResult, studentResult);
        Assert.Equal(staffResult, unknownResult);

        var message = await dbContext.OutboxMessages.SingleAsync(cancellationToken);
        Assert.Equal("notification.events.default", message.Exchange);
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(message));
        Assert.Equal("staff@example.com", payload.RootElement.GetProperty("destinatario").GetString());
        Assert.Equal("recuperacao-de-senha", payload.RootElement.GetProperty("finalidade").GetString());
        Assert.Equal("recuperacao-de-senha", payload.RootElement.GetProperty("modelo").GetString());

        var link = payload.RootElement.GetProperty("dados").GetProperty("link").GetString()!;
        Assert.StartsWith("https://staff.example.test/admin/redefinir-senha?token=", link, StringComparison.Ordinal);
        var rawToken = ExtractToken(link);
        var storedToken = await dbContext.VerificationTokens.SingleAsync(cancellationToken);
        Assert.Equal(staffAccount.Id, storedToken.AccountId);
        Assert.Equal(HashToken(rawToken), storedToken.TokenHash);
        Assert.Single(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffPasswordRecoveryRequest_ReplaysIdempotentlyAndRejectsAChangedEmail))]
    [Trait("Layer", "Identity staff password recovery request - Integration")]
    public async Task StaffPasswordRecoveryRequest_ReplaysIdempotentlyAndRejectsAChangedEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var staffAccount = Account.CreateInternal(
            Guid.CreateVersion7(),
            tenantId,
            "Marina Alves",
            "staff@example.com",
            "staff@example.com");
        dbContext.Accounts.Add(staffAccount);
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var useCase = CreateRequestUseCase(dbContext);
        var request = new RequestStaffPasswordResetInput(tenantId, "staff@example.com", "staff-recovery-replay-1");
        var first = await useCase.ExecuteAsync(request, cancellationToken);
        var replay = await useCase.ExecuteAsync(request with { Email = "STAFF@example.com" }, cancellationToken);
        var conflict = await Assert.ThrowsAsync<StaffPasswordRecoveryException>(() => useCase.ExecuteAsync(
            request with { Email = "other@example.com" },
            cancellationToken));

        Assert.Equal(new RequestStaffPasswordResetOutput(202), first);
        Assert.Equal(first, replay);
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflict.Code);
        Assert.Single(await dbContext.VerificationTokens.ToListAsync(cancellationToken));
        Assert.Single(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffPasswordRecoveryRequest_EnforcesContractEmailAndIdempotencyLimits))]
    [Trait("Layer", "Identity staff password recovery request - Integration")]
    public async Task StaffPasswordRecoveryRequest_EnforcesContractEmailAndIdempotencyLimits()
    {
        var validator = new RequestStaffPasswordResetInputValidator();
        var tenantId = Guid.CreateVersion7();
        var cancellationToken = TestContext.Current.CancellationToken;
        var emailTooLong = await validator.ValidateAsync(
            new RequestStaffPasswordResetInput(tenantId, $"{new string('a', 243)}@example.com", "valid-key"),
            cancellationToken);
        var idempotencyKeyTooLong = await validator.ValidateAsync(
            new RequestStaffPasswordResetInput(tenantId, "staff@example.com", new string('k', 129)),
            cancellationToken);

        Assert.False(emailTooLong.IsValid);
        Assert.False(idempotencyKeyTooLong.IsValid);
    }

    private static RequestStaffPasswordReset CreateRequestUseCase(IdentityDbContext dbContext)
    {
        var staffOptions = CreateStaffAccountOptions();
        var destinations = Destinations();
        var messageWriter = new StaffPasswordRecoveryMessageWriter(
            new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector),
            staffOptions,
            destinations);
        return new RequestStaffPasswordReset(
            new IdentityPasswordRecoveryStore(dbContext),
            messageWriter,
            new IdentityUnitOfWork(dbContext),
            CreateFingerprinter(),
            staffOptions,
            TimeProvider.System,
            new RequestStaffPasswordResetInputValidator());
    }

    private static IOptions<StaffAccountOptions> CreateStaffAccountOptions()
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

    private static string ExtractToken(string link)
        => Uri.UnescapeDataString(new Uri(link).Query.TrimStart('?').Split("token=", 2, StringSplitOptions.None)[1]);

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
