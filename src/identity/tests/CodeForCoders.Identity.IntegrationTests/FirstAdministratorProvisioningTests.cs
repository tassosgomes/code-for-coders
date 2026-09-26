using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.ProvisionFirstAdministrator;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class FirstAdministratorProvisioningTests(IdentityIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(ProvisionFirstAdministrator_CreatesConfirmedAccountRoleAndProtectedResetRequest))]
    [Trait("Layer", "Identity staff account - Integration")]
    public async Task ProvisionFirstAdministrator_CreatesConfirmedAccountRoleAndProtectedResetRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using (var dbContext = fixture.CreateDbContext(tenantId))
        {
            var result = await CreateProvisionUseCase(dbContext).ExecuteAsync(
                new ProvisionFirstAdministratorInput(tenantId, "admin@example.com", "First Admin"),
                cancellationToken);
            Assert.Equal(ProvisionFirstAdministratorStatus.Created, result.Status);
        }

        await using var verifyContext = fixture.CreateDbContext(tenantId);
        var account = await verifyContext.Accounts.SingleAsync(cancellationToken);
        var role = await verifyContext.StaffRoleAssignments.SingleAsync(cancellationToken);
        var token = await verifyContext.VerificationTokens.SingleAsync(cancellationToken);
        var outbox = await verifyContext.OutboxMessages.SingleAsync(cancellationToken);

        Assert.Equal(AccountType.InternalActor, account.Type);
        Assert.True(account.IsConfirmed);
        Assert.Empty(await verifyContext.Credentials.ToListAsync(cancellationToken));
        Assert.Equal(StaffRoleCatalog.Administrator, role.Role);
        Assert.Equal([StaffRoleCatalog.ManageAccess], StaffRoleCatalog.GetPermissions(role.Role));
        Assert.Equal("notificacao.envio-solicitado.v1", outbox.RoutingKey);
        Assert.Equal("notification.events.default", outbox.Exchange);
        AssertPayloadProtected(outbox.Payload);

        using var message = JsonDocument.Parse(OutboxTestProtection.ReadPayload(outbox));
        var request = message.RootElement;
        var link = request.GetProperty("dados").GetProperty("link").GetString()!;
        var rawToken = ReadToken(link);
        Assert.Equal("admin@example.com", request.GetProperty("destinatario").GetString());
        Assert.Equal("recuperacao-de-senha", request.GetProperty("finalidade").GetString());
        Assert.StartsWith("https://staff.example.test/admin/redefinir-senha?token=", link, StringComparison.Ordinal);
        Assert.Equal(HashToken(rawToken), token.TokenHash);
        Assert.DoesNotContain("admin@example.com", outbox.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await verifyContext.OutboxMessages
            .Where(message => message.RoutingKey == "auditoria.ato-praticado.v1")
            .ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(ProvisionFirstAdministrator_IsIdempotentWhenTenantAlreadyHasAdministrator))]
    [Trait("Layer", "Identity staff account - Integration")]
    public async Task ProvisionFirstAdministrator_IsIdempotentWhenTenantAlreadyHasAdministrator()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateProvisionUseCase(dbContext);
        var input = new ProvisionFirstAdministratorInput(tenantId, "admin@example.com", "First Admin");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var second = await useCase.ExecuteAsync(input with { Email = "different@example.com" }, cancellationToken);

        Assert.Equal(ProvisionFirstAdministratorStatus.Created, first.Status);
        Assert.Equal(ProvisionFirstAdministratorStatus.AlreadyProvisioned, second.Status);
        await using var verifyContext = fixture.CreateDbContext(tenantId);
        Assert.Single(await verifyContext.Accounts.ToListAsync(cancellationToken));
        Assert.Single(await verifyContext.StaffRoleAssignments.ToListAsync(cancellationToken));
        Assert.Single(await verifyContext.VerificationTokens.ToListAsync(cancellationToken));
        Assert.Single(await verifyContext.OutboxMessages.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(ProvisionFirstAdministrator_RejectsEmailBelongingToStudentWithoutWritingData))]
    [Trait("Layer", "Identity staff account - Integration")]
    public async Task ProvisionFirstAdministrator_RejectsEmailBelongingToStudentWithoutWritingData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(Account.CreateStudent(
            Guid.CreateVersion7(),
            tenantId,
            "Student",
            "student@example.com",
            "student@example.com"));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var result = await CreateProvisionUseCase(dbContext).ExecuteAsync(
            new ProvisionFirstAdministratorInput(tenantId, "STUDENT@example.com", "First Admin"),
            cancellationToken);

        Assert.Equal(ProvisionFirstAdministratorStatus.EmailBelongsToStudent, result.Status);
        await using var verifyContext = fixture.CreateDbContext(tenantId);
        Assert.Single(await verifyContext.Accounts.ToListAsync(cancellationToken));
        Assert.Empty(await verifyContext.StaffRoleAssignments.ToListAsync(cancellationToken));
        Assert.Empty(await verifyContext.VerificationTokens.ToListAsync(cancellationToken));
        Assert.Empty(await verifyContext.OutboxMessages.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(ProvisionFirstAdministrator_RejectsAnotherInternalAccountEmail))]
    [Trait("Layer", "Identity staff account - Integration")]
    public async Task ProvisionFirstAdministrator_RejectsAnotherInternalAccountEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(Account.CreateInternal(
            Guid.CreateVersion7(),
            tenantId,
            "Existing Staff",
            "staff@example.com",
            "staff@example.com"));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var result = await CreateProvisionUseCase(dbContext).ExecuteAsync(
            new ProvisionFirstAdministratorInput(tenantId, "staff@example.com", "First Admin"),
            cancellationToken);

        Assert.Equal(ProvisionFirstAdministratorStatus.EmailAlreadyInUse, result.Status);
        await using var verifyContext = fixture.CreateDbContext(tenantId);
        Assert.Single(await verifyContext.Accounts.ToListAsync(cancellationToken));
        Assert.Empty(await verifyContext.StaffRoleAssignments.ToListAsync(cancellationToken));
        Assert.Empty(await verifyContext.OutboxMessages.ToListAsync(cancellationToken));
    }

    private static ProvisionFirstAdministrator CreateProvisionUseCase(IdentityDbContext dbContext)
    {
        var destinations = Destinations();
        var options = StaffAccountOptions();
        var writer = new StaffPasswordRecoveryMessageWriter(
            new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector),
            options,
            destinations);
        return new ProvisionFirstAdministrator(
            new IdentityStaffAccountStore(dbContext),
            new IdentityPasswordRecoveryStore(dbContext),
            writer,
            new IdentityUnitOfWork(dbContext),
            options,
            TimeProvider.System,
            new ProvisionFirstAdministratorInputValidator());
    }

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

    private static string ReadToken(string link)
        => Uri.UnescapeDataString(new Uri(link).Query.TrimStart('?').Split("token=", 2, StringSplitOptions.None)[1]);

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static void AssertPayloadProtected(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        Assert.Equal(OutboxPayloadProtector.Algorithm, document.RootElement.GetProperty("$protected").GetString());
    }
}
