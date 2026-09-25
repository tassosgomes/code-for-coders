using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.CreateStaffInvitation;
using CodeForCoders.Identity.Application.UseCases.Accounts.ListPendingStaffInvitations;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Configuration;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StaffInvitationIssuingTests(IdentityIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(StaffInvitation_CreatesHashedInviteAndProtectedMessagesForNotificationAndAudit))]
    public async Task StaffInvitation_CreatesHashedInviteAndProtectedMessagesForNotificationAndAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "admin@example.com", cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var result = await CreateUseCase(dbContext).ExecuteAsync(
            Input(tenantId, actorId, "  Guest@Example.com  ", "professor", "Contratada para a trilha avançada.", "staff-invitation-create"),
            cancellationToken);

        Assert.Equal("Guest@Example.com", result.Email);
        Assert.Equal("professor", result.OfferedRole);
        Assert.Null(result.SupersededInvitationId);
        var invitation = await dbContext.StaffInvitations.SingleAsync(cancellationToken);
        Assert.Equal("guest@example.com", invitation.NormalizedEmail);
        Assert.Equal(64, invitation.TokenHash.Length);
        Assert.Equal(result.ExpiresAt, result.InvitedAt.AddHours(168));

        var messages = await dbContext.OutboxMessages.OrderBy(message => message.RoutingKey).ToListAsync(cancellationToken);
        Assert.Equal(2, messages.Count);
        var notification = Assert.Single(messages, message => message.RoutingKey == "notificacao.envio-solicitado.v1");
        var audit = Assert.Single(messages, message => message.RoutingKey == "auditoria.ato-praticado.v1");
        Assert.Equal("notification.events.default", notification.Exchange);
        Assert.Equal("audit.events", audit.Exchange);
        Assert.DoesNotContain("Guest@Example.com", notification.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Guest@Example.com", audit.Payload, StringComparison.Ordinal);
        Assert.Contains("$protected", notification.Payload, StringComparison.Ordinal);
        Assert.Contains("$protected", audit.Payload, StringComparison.Ordinal);

        var protector = CreateOutboxPayloadProtector();
        using var notificationDocument = JsonDocument.Parse(protector.Unprotect(notification));
        var notificationPayload = notificationDocument.RootElement;
        Assert.Equal(notification.Id, notificationPayload.GetProperty("pedidoId").GetGuid());
        Assert.Equal(tenantId, notificationPayload.GetProperty("tenantId").GetGuid());
        Assert.Equal("convite-interno", notificationPayload.GetProperty("finalidade").GetString());
        Assert.Equal("convite-interno", notificationPayload.GetProperty("modelo").GetString());
        var data = notificationPayload.GetProperty("dados");
        Assert.Equal("professor", data.GetProperty("papel").GetString());
        Assert.False(data.TryGetProperty("nome", out _));
        var link = data.GetProperty("link").GetString();
        Assert.StartsWith("http://localhost:8081/admin/convite?token=", link, StringComparison.Ordinal);
        var rawToken = new Uri(link!).Query["?token=".Length..];
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(rawToken)))),
            invitation.TokenHash);

        using var auditDocument = JsonDocument.Parse(protector.Unprotect(audit));
        var auditPayload = auditDocument.RootElement;
        Assert.Equal(audit.Id, auditPayload.GetProperty("fatoId").GetGuid());
        Assert.Equal("identidade", auditPayload.GetProperty("origem").GetString());
        Assert.Equal("convite-interno-emitido", auditPayload.GetProperty("tipo").GetString());
        Assert.Equal(tenantId, auditPayload.GetProperty("tenantId").GetGuid());
        Assert.Equal("conta-interna", auditPayload.GetProperty("autor").GetProperty("tipo").GetString());
        Assert.Equal(actorId, auditPayload.GetProperty("autor").GetProperty("id").GetGuid());
        Assert.Equal("convite-interno", auditPayload.GetProperty("alvo").GetProperty("tipo").GetString());
        Assert.Equal(result.InvitationId, auditPayload.GetProperty("alvo").GetProperty("id").GetGuid());
        Assert.Equal("professor", auditPayload.GetProperty("complemento").GetProperty("papel").GetString());
        Assert.Equal("Contratada para a trilha avançada.", auditPayload.GetProperty("motivo").GetString());
        Assert.Equal(9, auditPayload.EnumerateObject().Count());
        Assert.DoesNotContain("email", auditPayload.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = nameof(StaffInvitation_ReplaysTheCreatedResultForTheSameIdempotencyKey))]
    public async Task StaffInvitation_ReplaysTheCreatedResultForTheSameIdempotencyKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "admin@example.com", cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateUseCase(dbContext);
        var input = Input(tenantId, actorId, "guest@example.com", "suporte", "Equipe de suporte.", "staff-invitation-replay");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);

        Assert.Equal(first.InvitationId, replay.InvitationId);
        Assert.Equal(first.Email, replay.Email);
        Assert.Equal(first.OfferedRole, replay.OfferedRole);
        Assert.Equal(first.InvitedAt, replay.InvitedAt);
        Assert.Equal(first.ExpiresAt, replay.ExpiresAt);
        Assert.Equal(first.SupersededInvitationId, replay.SupersededInvitationId);
        Assert.Single(await dbContext.StaffInvitations.ToListAsync(cancellationToken));
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(cancellationToken));
        Assert.Single(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffInvitation_ReplacesThePreviousUnresolvedInvitation))]
    public async Task StaffInvitation_ReplacesThePreviousUnresolvedInvitation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "admin@example.com", cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateUseCase(dbContext);

        var first = await useCase.ExecuteAsync(
            Input(tenantId, actorId, "guest@example.com", "professor", "Motivo inicial.", "staff-invitation-first"),
            cancellationToken);
        var second = await useCase.ExecuteAsync(
            Input(tenantId, actorId, "GUEST@example.com", "suporte", "Mudança de função.", "staff-invitation-second"),
            cancellationToken);

        Assert.Equal(first.InvitationId, second.SupersededInvitationId);
        Assert.Equal(2, await dbContext.StaffInvitations.CountAsync(cancellationToken));
        var superseded = await dbContext.StaffInvitations.SingleAsync(item => item.Id == first.InvitationId, cancellationToken);
        Assert.NotNull(superseded.SupersededOn);
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(message => message.RoutingKey == "auditoria.ato-praticado.v1", cancellationToken));
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(message => message.RoutingKey == "notificacao.envio-solicitado.v1", cancellationToken));
    }

    [Fact(DisplayName = nameof(StaffInvitation_RejectsAnEmailThatBelongsToAnInternalAccountWithoutWrites))]
    public async Task StaffInvitation_RejectsAnEmailThatBelongsToAnInternalAccountWithoutWrites()
    {
        await AssertAccountEmailRejectedAsync(
            student: false,
            expectedCode: "EMAIL_BELONGS_TO_STAFF",
            testName: nameof(StaffInvitation_RejectsAnEmailThatBelongsToAnInternalAccountWithoutWrites));
    }

    [Fact(DisplayName = nameof(StaffInvitation_RejectsAnEmailThatBelongsToAStudentWithoutWrites))]
    public async Task StaffInvitation_RejectsAnEmailThatBelongsToAStudentWithoutWrites()
    {
        await AssertAccountEmailRejectedAsync(
            student: true,
            expectedCode: "EMAIL_BELONGS_TO_STUDENT",
            testName: nameof(StaffInvitation_RejectsAnEmailThatBelongsToAStudentWithoutWrites));
    }

    [Fact(DisplayName = nameof(StaffInvitation_RejectsAnEmptyReasonWithoutWrites))]
    public async Task StaffInvitation_RejectsAnEmptyReasonWithoutWrites()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "admin@example.com", cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<StaffInvitationException>(() => CreateUseCase(dbContext).ExecuteAsync(
            Input(tenantId, actorId, "guest@example.com", "professor", "   ", "staff-invitation-empty-reason"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal("REASON_REQUIRED", exception.Code);
        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffInvitation_RejectsAReasonLongerThanTheContractLimitWithoutWrites))]
    public async Task StaffInvitation_RejectsAReasonLongerThanTheContractLimitWithoutWrites()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "admin@example.com", cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);

        var exception = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => CreateUseCase(dbContext).ExecuteAsync(
            Input(tenantId, actorId, "guest@example.com", "professor", new string('m', 1001), "staff-invitation-long-reason"),
            cancellationToken));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(CreateStaffInvitationInput.Reason));
        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    [Fact(DisplayName = nameof(StaffInvitation_ListsOnlyUnexpiredPendingInvitationsFromTheTenant))]
    public async Task StaffInvitation_ListsOnlyUnexpiredPendingInvitationsFromTheTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "admin@example.com", cancellationToken);
        var otherActorId = await SeedInternalAsync(otherTenantId, "other-admin@example.com", cancellationToken);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var create = CreateUseCase(dbContext);
        await create.ExecuteAsync(Input(tenantId, actorId, "first@example.com", "professor", "First.", "staff-invitation-list-first"), cancellationToken);
        await create.ExecuteAsync(Input(tenantId, actorId, "second@example.com", "suporte", "Second.", "staff-invitation-list-second"), cancellationToken);
        await using (var otherDbContext = fixture.CreateDbContext(otherTenantId))
        {
            await CreateUseCase(otherDbContext).ExecuteAsync(
                Input(otherTenantId, otherActorId, "other@example.com", "financeiro", "Other tenant.", "staff-invitation-list-other"),
                cancellationToken);
        }
        var oldInvitation = StaffInvitation.Create(
            Guid.CreateVersion7(),
            tenantId,
            "expired@example.com",
            "expired@example.com",
            "professor",
            new string('A', 64),
            DateTimeOffset.UtcNow.AddDays(-8),
            DateTimeOffset.UtcNow.AddDays(-1));
        dbContext.StaffInvitations.Add(oldInvitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await new ListPendingStaffInvitations(
            new IdentityStaffInvitationStore(dbContext),
            TimeProvider.System,
            new ListPendingStaffInvitationsInputValidator()).ExecuteAsync(
            new ListPendingStaffInvitationsInput(tenantId, 1, 1),
            cancellationToken);

        Assert.Equal(2, result.Pagination.Total);
        Assert.Equal(2, result.Pagination.TotalPages);
        Assert.Single(result.Data);
        Assert.Equal("second@example.com", result.Data[0].Email);
    }

    private async Task AssertAccountEmailRejectedAsync(bool student, string expectedCode, string testName)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        var actorId = await SeedInternalAsync(tenantId, "admin@example.com", cancellationToken);
        if (student)
        {
            await SeedStudentAsync(tenantId, "guest@example.com", cancellationToken);
        }
        else
        {
            await SeedInternalAsync(tenantId, "guest@example.com", cancellationToken);
        }

        await using var dbContext = fixture.CreateDbContext(tenantId);
        var exception = await Assert.ThrowsAsync<StaffInvitationException>(() => CreateUseCase(dbContext).ExecuteAsync(
            Input(tenantId, actorId, "guest@example.com", "professor", "Acesso necessário.", $"staff-invitation-{testName}"),
            cancellationToken));

        Assert.Equal(422, exception.StatusCode);
        Assert.Equal(expectedCode, exception.Code);
        await AssertNoSideEffectsAsync(dbContext, cancellationToken);
    }

    private async Task<Guid> SeedInternalAsync(Guid tenantId, string email, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var accountId = Guid.CreateVersion7(now);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(Account.CreateInternal(accountId, tenantId, "Staff Actor", email, email.ToLowerInvariant()));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        return accountId;
    }

    private async Task SeedStudentAsync(Guid tenantId, string email, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var accountId = Guid.CreateVersion7(now);
        await using var dbContext = fixture.CreateDbContext(tenantId);
        dbContext.Accounts.Add(Account.CreateStudent(accountId, tenantId, "Student", email, email.ToLowerInvariant()));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
    }

    private static CreateStaffInvitation CreateUseCase(IdentityDbContext dbContext)
    {
        var destinations = Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            NotificationExchange = "notification.events.default",
            AuditExchange = "audit.events",
        });
        var invitationOptions = Options.Create(new StaffInvitationOptions
        {
            AcceptanceBaseUrl = "http://localhost:8081/admin/convite",
            LifetimeHours = 168,
        });
        var writer = new StaffInvitationMessageWriter(
            new OutboxMessageWriter(dbContext, destinations, CreateOutboxPayloadProtector()),
            invitationOptions,
            destinations);
        return new CreateStaffInvitation(
            new IdentityStaffAccountStore(dbContext),
            new IdentityStaffInvitationStore(dbContext),
            new IdentitySessionStore(dbContext),
            writer,
            new IdentityUnitOfWork(dbContext),
            CreateFingerprinter(),
            invitationOptions,
            TimeProvider.System,
            new CreateStaffInvitationInputValidator());
    }

    private static CreateStaffInvitationInput Input(
        Guid tenantId,
        Guid actorId,
        string email,
        string role,
        string reason,
        string idempotencyKey)
        => new(tenantId, actorId, email, role, reason, idempotencyKey);

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

    private static async Task AssertNoSideEffectsAsync(IdentityDbContext dbContext, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        Assert.Empty(await dbContext.StaffInvitations.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.IdempotencyRecords.ToListAsync(cancellationToken));
    }
}
