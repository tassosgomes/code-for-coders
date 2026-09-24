using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentAccountConfirmation;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StudentConfirmationTests(IdentityIntegrationFixture fixture)
{
    [Fact]
    public async Task StudentConfirmation_ConsumesTokenOnceAndPublishesTheFactInTheSameCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var token = await RegisterAndGetTokenAsync(dbContext, tenantId, "confirmation-1", cancellationToken);
        var useCase = CreateConfirmUseCase(dbContext);
        var input = new ConfirmStudentAccountInput(tenantId, token, "confirm-1");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);
        var conflict = await Assert.ThrowsAsync<StudentConfirmationException>(() =>
            useCase.ExecuteAsync(input with { Token = token + "altered" }, cancellationToken));
        var secondIntent = await Assert.ThrowsAsync<StudentConfirmationException>(() =>
            useCase.ExecuteAsync(input with { IdempotencyKey = "confirm-1b" }, cancellationToken));

        Assert.Equal(204, first.StatusCode);
        Assert.Equal(204, replay.StatusCode);
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflict.Code);
        Assert.Equal("INVALID_VERIFICATION_TOKEN", secondIntent.Code);
        await using var verificationContext = fixture.CreateDbContext(tenantId);
        var account = await verificationContext.Accounts.SingleAsync(cancellationToken);
        var storedToken = await verificationContext.VerificationTokens.SingleAsync(cancellationToken);
        var fact = await verificationContext.OutboxMessages.SingleAsync(
            message => message.RoutingKey == "identidade.conta-confirmada.v1",
            cancellationToken);
        Assert.True(account.IsConfirmed);
        Assert.NotNull(storedToken.ConsumedOn);
        Assert.Equal("identity.events", fact.Exchange);
        Assert.DoesNotContain(account.Email, fact.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, fact.Payload, StringComparison.Ordinal);
        Assert.Equal(1, await verificationContext.OutboxMessages.CountAsync(
            message => message.RoutingKey == "identidade.conta-confirmada.v1",
            cancellationToken));
    }

    [Fact]
    public async Task StudentConfirmation_RejectsAlteredExpiredAndWrongPurposeTokensWithoutChangingTheAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var validToken = await RegisterAndGetTokenAsync(dbContext, tenantId, "confirmation-2", cancellationToken);
        var account = await dbContext.Accounts.SingleAsync(cancellationToken);
        var now = TimeProvider.System.GetUtcNow();
        dbContext.VerificationTokens.AddRange(
            VerificationToken.Create(
                Guid.CreateVersion7(now.AddTicks(1)),
                tenantId,
                account.Id,
                "recuperacao-de-senha",
                HashToken("wrong-purpose-token"),
                now.AddHours(1)),
            VerificationToken.Create(
                Guid.CreateVersion7(now.AddTicks(2)),
                tenantId,
                account.Id,
                "confirmacao-de-conta",
                HashToken("expired-token"),
                now.AddHours(-1)));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        var useCase = CreateConfirmUseCase(dbContext);

        foreach (var (token, key) in new[]
        {
            (validToken + "altered", "confirmation-2a"),
            ("wrong-purpose-token", "confirmation-2b"),
            ("expired-token", "confirmation-2c"),
        })
        {
            var exception = await Assert.ThrowsAsync<StudentConfirmationException>(() =>
                useCase.ExecuteAsync(new ConfirmStudentAccountInput(tenantId, token, key), cancellationToken));
            Assert.Equal("INVALID_VERIFICATION_TOKEN", exception.Code);
        }

        await using var verificationContext = fixture.CreateDbContext(tenantId);
        Assert.False((await verificationContext.Accounts.SingleAsync(cancellationToken)).IsConfirmed);
        Assert.Equal(2, await verificationContext.OutboxMessages.CountAsync(cancellationToken));
        Assert.Empty(await verificationContext.OutboxMessages.Where(
            message => message.RoutingKey == "identidade.conta-confirmada.v1").ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task StudentConfirmation_ConcurrentAttemptsConfirmOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        string token;
        await using (var registrationContext = fixture.CreateDbContext(tenantId))
        {
            token = await RegisterAndGetTokenAsync(registrationContext, tenantId, "confirmation-3", cancellationToken);
        }

        await using var firstContext = fixture.CreateDbContext(tenantId);
        await using var secondContext = fixture.CreateDbContext(tenantId);
        var outcomes = await Task.WhenAll(
            ConfirmOutcomeAsync(CreateConfirmUseCase(firstContext), new ConfirmStudentAccountInput(tenantId, token, "confirm-3a"), cancellationToken),
            ConfirmOutcomeAsync(CreateConfirmUseCase(secondContext), new ConfirmStudentAccountInput(tenantId, token, "confirm-3b"), cancellationToken));

        Assert.Contains("confirmed", outcomes);
        Assert.Contains("INVALID_VERIFICATION_TOKEN", outcomes);
        await using var verificationContext = fixture.CreateDbContext(tenantId);
        Assert.True((await verificationContext.Accounts.SingleAsync(cancellationToken)).IsConfirmed);
        Assert.Equal(1, await verificationContext.OutboxMessages.CountAsync(
            message => message.RoutingKey == "identidade.conta-confirmada.v1",
            cancellationToken));
    }

    [Fact]
    public async Task StudentConfirmationRequest_IsIdempotentAndDoesNotRevealWhetherTheEmailExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        await RegisterAndGetTokenAsync(dbContext, tenantId, "confirmation-4", cancellationToken);
        var useCase = CreateRequestUseCase(dbContext);
        var request = new RequestStudentAccountConfirmationInput(tenantId, "STUDENT@example.com", "resend-4");

        var first = await useCase.ExecuteAsync(request, cancellationToken);
        var replay = await useCase.ExecuteAsync(request, cancellationToken);
        var unknown = await useCase.ExecuteAsync(
            request with { Email = "unknown@example.com", IdempotencyKey = "resend-4b" },
            cancellationToken);

        Assert.Equal(202, first.StatusCode);
        Assert.Equal(202, replay.StatusCode);
        Assert.Equal(202, unknown.StatusCode);
        Assert.Equal(1, await dbContext.Accounts.CountAsync(cancellationToken));
        Assert.Equal(1, await dbContext.Credentials.CountAsync(cancellationToken));
        Assert.Equal(2, await dbContext.VerificationTokens.CountAsync(cancellationToken));
        var requests = await dbContext.OutboxMessages
            .Where(message => message.RoutingKey == "notificacao.envio-solicitado.v1")
            .OrderBy(message => message.OccurredOn)
            .ToListAsync(cancellationToken);
        Assert.Equal(2, requests.Count);
        Assert.Equal("notification.events.default", requests[^1].Exchange);
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(requests[^1]));
        Assert.Equal("student@example.com", payload.RootElement.GetProperty("destinatario").GetString());
        Assert.Equal("confirmacao-de-conta", payload.RootElement.GetProperty("finalidade").GetString());
        Assert.Equal("confirmacao-de-conta", payload.RootElement.GetProperty("modelo").GetString());
        var link = payload.RootElement.GetProperty("dados").GetProperty("link").GetString();
        Assert.Contains("token=", link, StringComparison.Ordinal);
        var resentToken = Uri.UnescapeDataString(new Uri(link!).Query.TrimStart('?').Split("token=", 2)[1]);
        var tokenHashes = await dbContext.VerificationTokens.Select(token => token.TokenHash).ToListAsync(cancellationToken);
        Assert.Contains(HashToken(resentToken), tokenHashes);
        Assert.All(tokenHashes, tokenHash => Assert.NotEqual(resentToken, tokenHash));
    }

    private static async Task<string> RegisterAndGetTokenAsync(
        IdentityDbContext dbContext,
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await CreateRegistrationUseCase(dbContext).ExecuteAsync(
            new RegisterStudentAccountInput(tenantId, "Ana Souza", "student@example.com", "SenhaForte1!", idempotencyKey),
            cancellationToken);
        var request = await dbContext.OutboxMessages.SingleAsync(
            message => message.RoutingKey == "notificacao.envio-solicitado.v1",
            cancellationToken);
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(request));
        var link = payload.RootElement.GetProperty("dados").GetProperty("link").GetString()!;
        return Uri.UnescapeDataString(new Uri(link).Query.TrimStart('?').Split("token=", 2)[1]);
    }

    private static ConfirmStudentAccount CreateConfirmUseCase(IdentityDbContext dbContext)
        => new(
            new IdentityConfirmationStore(dbContext),
            CreateMessageWriter(dbContext),
            new IdentityUnitOfWork(dbContext),
            CreateFingerprinter(),
            TimeProvider.System,
            new ConfirmStudentAccountInputValidator());

    private static RequestStudentAccountConfirmation CreateRequestUseCase(IdentityDbContext dbContext)
        => new(
            new IdentityConfirmationStore(dbContext),
            CreateMessageWriter(dbContext),
            new IdentityUnitOfWork(dbContext),
            CreateFingerprinter(),
            RegistrationOptions(),
            TimeProvider.System,
            new RequestStudentAccountConfirmationInputValidator());

    private static RegisterStudentAccount CreateRegistrationUseCase(IdentityDbContext dbContext)
    {
        var destinations = Destinations();
        return new RegisterStudentAccount(
            new IdentityRegistrationStore(dbContext),
            new StudentRegistrationMessageWriter(new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector), RegistrationOptions(), destinations),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            CreateFingerprinter(),
            RegistrationOptions(),
            TimeProvider.System,
            new RegisterStudentAccountInputValidator());
    }

    private static StudentConfirmationMessageWriter CreateMessageWriter(IdentityDbContext dbContext)
    {
        var destinations = Destinations();
        return new StudentConfirmationMessageWriter(
            new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector),
            RegistrationOptions(),
            destinations);
    }

    private static IIdempotencyFingerprinter CreateFingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));

    private static IOptions<RegistrationOptions> RegistrationOptions()
        => Options.Create(new RegistrationOptions
        {
            ConfirmationBaseUrl = "https://students.example.test/confirm-account",
            ConfirmationLifetimeHours = 24,
        });

    private static IOptions<OutboxDestinationOptions> Destinations()
        => Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            NotificationExchange = "notification.events.default",
        });

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static async Task<string> ConfirmOutcomeAsync(
        ConfirmStudentAccount useCase,
        ConfirmStudentAccountInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            var output = await useCase.ExecuteAsync(input, cancellationToken);
            return output.StatusCode == 204 ? "confirmed" : "unexpected";
        }
        catch (StudentConfirmationException exception)
        {
            return exception.Code;
        }
    }
}
