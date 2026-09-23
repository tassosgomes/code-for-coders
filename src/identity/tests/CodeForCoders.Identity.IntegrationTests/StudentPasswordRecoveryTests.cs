using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentPasswordReset;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStudentPassword;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Outbox;
using CodeForCoders.Identity.Infra.Messaging;
using CodeForCoders.Identity.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(IdentityIntegrationCollection.Name)]
public sealed class StudentPasswordRecoveryTests(IdentityIntegrationFixture fixture)
{
    [Fact]
    public async Task StudentPasswordRecoveryRequest_IsNeutralAndQueuesOneHashedRecoveryTokenForAnEligibleStudent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        await RegisterAndGetConfirmationTokenAsync(dbContext, tenantId, "recovery-request-registration", cancellationToken);
        var account = await dbContext.Accounts.SingleAsync(cancellationToken);
        account.Confirm();
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var useCase = CreateRequestUseCase(dbContext);
        var eligibleInput = new RequestStudentPasswordResetInput(tenantId, "STUDENT@example.com", "recovery-request-1");
        var eligible = await useCase.ExecuteAsync(eligibleInput, cancellationToken);
        var replay = await useCase.ExecuteAsync(eligibleInput, cancellationToken);
        var unknown = await useCase.ExecuteAsync(
            eligibleInput with { Email = "unknown@example.com", IdempotencyKey = "recovery-request-2" },
            cancellationToken);

        Assert.Equal(202, eligible.StatusCode);
        Assert.Equal(eligible, replay);
        Assert.Equal(202, unknown.StatusCode);
        var requestMessage = (await GetRecoveryMessagesAsync(dbContext, cancellationToken)).Single();
        Assert.Equal("notification.events.default", requestMessage.Exchange);
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(requestMessage));
        Assert.Equal(requestMessage.Id, payload.RootElement.GetProperty("pedidoId").GetGuid());
        Assert.Equal("student@example.com", payload.RootElement.GetProperty("destinatario").GetString());
        Assert.Equal("recuperacao-de-senha", payload.RootElement.GetProperty("finalidade").GetString());
        Assert.Equal("recuperacao-de-senha", payload.RootElement.GetProperty("modelo").GetString());

        var rawToken = ExtractToken(payload.RootElement.GetProperty("dados").GetProperty("link").GetString()!);
        var storedToken = await dbContext.VerificationTokens.SingleAsync(
            token => token.Purpose == "recuperacao-de-senha",
            cancellationToken);
        Assert.Equal(HashToken(rawToken), storedToken.TokenHash);
        Assert.NotEqual(rawToken, storedToken.TokenHash);
        Assert.Equal(requestMessage.OccurredOn.AddHours(1), storedToken.ExpiresOn);
        Assert.Single(await GetRecoveryMessagesAsync(dbContext, cancellationToken));

        await using var persistedContext = fixture.CreateDbContext(tenantId);
        var persisted = await persistedContext.OutboxMessages.AsNoTracking().SingleAsync(
            message => message.Id == requestMessage.Id,
            cancellationToken);
        AssertPayloadIsProtected(persisted.Payload, rawToken);
    }

    [Fact]
    public async Task StudentPasswordRecoveryRequest_PublishesTheDecryptedLinkWithTheSameRequestIdOnRetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        await RegisterAndGetConfirmationTokenAsync(dbContext, tenantId, "recovery-publish-registration", cancellationToken);
        var rawToken = await RequestResetAsync(dbContext, tenantId, "recovery-publish-request", cancellationToken);
        var requestMessage = (await GetRecoveryMessagesAsync(dbContext, cancellationToken)).Single();
        AssertPayloadIsProtected(requestMessage.Payload, rawToken);

        await using var connectionProvider = new RabbitMqConnectionProvider(Options.Create(new RabbitMqOptions
        {
            Host = fixture.RabbitMq.Hostname,
            Port = fixture.RabbitMq.GetMappedPublicPort(5672),
            Username = "code_for_coders",
            Password = "code_for_coders",
        }));
        await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            requestMessage.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue.QueueName,
            requestMessage.Exchange,
            requestMessage.RoutingKey,
            cancellationToken: cancellationToken);

        var publisher = new RabbitMqPublisher(connectionProvider, OutboxTestProtection.Protector);
        await publisher.PublishAsync(requestMessage, cancellationToken);
        await publisher.PublishAsync(requestMessage, cancellationToken);

        for (var delivery = 0; delivery < 2; delivery++)
        {
            var received = await channel.BasicGetAsync(queue.QueueName, autoAck: true, cancellationToken);
            Assert.NotNull(received);
            Assert.Equal(requestMessage.Id.ToString(), received.BasicProperties.MessageId);
            using var body = JsonDocument.Parse(received.Body.ToArray());
            Assert.Equal(requestMessage.Id, body.RootElement.GetProperty("pedidoId").GetGuid());
            Assert.Equal("student@example.com", body.RootElement.GetProperty("destinatario").GetString());
            Assert.Equal("recuperacao-de-senha", body.RootElement.GetProperty("modelo").GetString());
            var link = body.RootElement.GetProperty("dados").GetProperty("link").GetString()!;
            Assert.StartsWith("https://students.example.test/redefinir-senha?token=", link, StringComparison.Ordinal);
            Assert.Equal(rawToken, ExtractToken(link));
        }

        var storedToken = await dbContext.VerificationTokens.SingleAsync(
            token => token.Purpose == "recuperacao-de-senha",
            cancellationToken);
        Assert.Equal(HashToken(rawToken), storedToken.TokenHash);
    }

    [Fact]
    public async Task StudentPasswordRecoveryReset_ChangesCredentialOnceAndRevokesSessionsAndOtherLinksAtomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        await RegisterAndGetConfirmationTokenAsync(dbContext, tenantId, "recovery-reset-registration", cancellationToken);
        var account = await dbContext.Accounts.SingleAsync(cancellationToken);
        account.Confirm();
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        await RequestResetAsync(dbContext, tenantId, "recovery-reset-1", cancellationToken);
        await RequestResetAsync(dbContext, tenantId, "recovery-reset-2", cancellationToken);
        var recoveryMessages = await GetRecoveryMessagesAsync(dbContext, cancellationToken);
        var recoveryTokens = recoveryMessages.Select(message =>
        {
            using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(message));
            return ExtractToken(payload.RootElement.GetProperty("dados").GetProperty("link").GetString()!);
        }).ToArray();
        var oldPasswordHash = (await dbContext.Credentials.SingleAsync(cancellationToken)).PasswordHash;
        var now = TimeProvider.System.GetUtcNow();
        dbContext.StudentSessions.AddRange(
            StudentSession.Create(Guid.CreateVersion7(now), tenantId, account.Id, now, now.AddHours(1)),
            StudentSession.Create(Guid.CreateVersion7(now.AddTicks(1)), tenantId, account.Id, now, now.AddHours(1)));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var useCase = CreateResetUseCase(dbContext);
        var input = new ResetStudentPasswordInput(tenantId, recoveryTokens[0], "NovaSenha2!", "recovery-reset-3");
        var result = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);
        var conflict = await Assert.ThrowsAsync<StudentPasswordRecoveryException>(() =>
            useCase.ExecuteAsync(input with { NewPassword = "OutraSenha3!" }, cancellationToken));

        Assert.Equal(204, result.StatusCode);
        Assert.Equal(result, replay);
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflict.Code);

        await using var verificationContext = fixture.CreateDbContext(tenantId);
        var verifiedAccount = await verificationContext.Accounts.SingleAsync(cancellationToken);
        var credential = await verificationContext.Credentials.SingleAsync(cancellationToken);
        var sessions = await verificationContext.StudentSessions.OrderBy(session => session.CreatedOn).ToListAsync(cancellationToken);
        var tokens = await verificationContext.VerificationTokens
            .Where(token => token.Purpose == "recuperacao-de-senha")
            .ToListAsync(cancellationToken);
        var resetFact = await verificationContext.OutboxMessages.SingleAsync(
            message => message.RoutingKey == "identidade.senha-redefinida.v1",
            cancellationToken);

        Assert.True(verifiedAccount.IsConfirmed);
        Assert.NotEqual(oldPasswordHash, credential.PasswordHash);
        Assert.False(new Pbkdf2PasswordHasher().Verify("SenhaForte1!", credential.PasswordHash));
        Assert.True(new Pbkdf2PasswordHasher().Verify("NovaSenha2!", credential.PasswordHash));
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, session => Assert.NotNull(session.RevokedOn));
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, token => Assert.NotNull(token.ConsumedOn));
        Assert.Equal("identity.events", resetFact.Exchange);
        Assert.DoesNotContain("student@example.com", resetFact.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(recoveryTokens[0], resetFact.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("NovaSenha2!", resetFact.Payload, StringComparison.Ordinal);
        Assert.Equal(1, await verificationContext.OutboxMessages.CountAsync(
            message => message.RoutingKey == "identidade.senha-redefinida.v1",
            cancellationToken));
    }

    [Fact]
    public async Task StudentPasswordRecoveryReset_RejectsAWeakPasswordWithoutConsumingTheTokenOrRevokingSessions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        await RegisterAndGetConfirmationTokenAsync(dbContext, tenantId, "recovery-weak-registration", cancellationToken);
        var account = await dbContext.Accounts.SingleAsync(cancellationToken);
        var rawToken = await RequestResetAsync(dbContext, tenantId, "recovery-weak-request", cancellationToken);
        var originalHash = (await dbContext.Credentials.SingleAsync(cancellationToken)).PasswordHash;
        var now = TimeProvider.System.GetUtcNow();
        dbContext.StudentSessions.Add(StudentSession.Create(
            Guid.CreateVersion7(now), tenantId, account.Id, now, now.AddHours(1)));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);

        var useCase = CreateResetUseCase(dbContext);
        var rejected = await Assert.ThrowsAsync<StudentPasswordRecoveryException>(() => useCase.ExecuteAsync(
            new ResetStudentPasswordInput(tenantId, rawToken, "weak", "recovery-weak-reset-1"),
            cancellationToken));

        Assert.Equal("PASSWORD_RESET_REJECTED", rejected.Code);
        await using (var rejectedContext = fixture.CreateDbContext(tenantId))
        {
            Assert.Equal(originalHash, (await rejectedContext.Credentials.SingleAsync(cancellationToken)).PasswordHash);
            Assert.Null((await rejectedContext.VerificationTokens.SingleAsync(
                token => token.Purpose == "recuperacao-de-senha",
                cancellationToken)).ConsumedOn);
            Assert.Null((await rejectedContext.StudentSessions.SingleAsync(cancellationToken)).RevokedOn);
            Assert.Empty(await rejectedContext.OutboxMessages.Where(
                message => message.RoutingKey == "identidade.senha-redefinida.v1").ToListAsync(cancellationToken));
        }

        var accepted = await useCase.ExecuteAsync(
            new ResetStudentPasswordInput(tenantId, rawToken, "SenhaNova2!", "recovery-weak-reset-2"),
            cancellationToken);
        Assert.Equal(204, accepted.StatusCode);
        await using var verificationContext = fixture.CreateDbContext(tenantId);
        Assert.False((await verificationContext.Accounts.SingleAsync(cancellationToken)).IsConfirmed);
        var updatedHash = (await verificationContext.Credentials.SingleAsync(cancellationToken)).PasswordHash;
        Assert.False(new Pbkdf2PasswordHasher().Verify("SenhaForte1!", updatedHash));
        Assert.True(new Pbkdf2PasswordHasher().Verify("SenhaNova2!", updatedHash));
    }

    [Fact]
    public async Task StudentPasswordRecoveryReset_RejectsChangedExpiredAndWrongPurposeTokensWithoutChangingCredentialOrSessions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var confirmationToken = await RegisterAndGetConfirmationTokenAsync(
            dbContext,
            tenantId,
            "recovery-invalid-registration",
            cancellationToken);
        var account = await dbContext.Accounts.SingleAsync(cancellationToken);
        var now = TimeProvider.System.GetUtcNow();
        dbContext.VerificationTokens.Add(VerificationToken.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            tenantId,
            account.Id,
            "recuperacao-de-senha",
            HashToken("expired-recovery-token"),
            now.AddHours(-1)));
        dbContext.StudentSessions.Add(StudentSession.Create(
            Guid.CreateVersion7(now.AddTicks(2)), tenantId, account.Id, now, now.AddHours(1)));
        await new IdentityUnitOfWork(dbContext).CommitAsync(cancellationToken);
        var originalHash = (await dbContext.Credentials.SingleAsync(cancellationToken)).PasswordHash;
        var useCase = CreateResetUseCase(dbContext);

        foreach (var (token, key) in new[]
        {
            (confirmationToken + "altered", "recovery-invalid-1"),
            (confirmationToken, "recovery-invalid-2"),
            ("expired-recovery-token", "recovery-invalid-3"),
        })
        {
            var rejected = await Assert.ThrowsAsync<StudentPasswordRecoveryException>(() => useCase.ExecuteAsync(
                new ResetStudentPasswordInput(tenantId, token, "SenhaNova2!", key),
                cancellationToken));
            Assert.Equal("PASSWORD_RESET_REJECTED", rejected.Code);
        }

        await using var verificationContext = fixture.CreateDbContext(tenantId);
        Assert.Equal(originalHash, (await verificationContext.Credentials.SingleAsync(cancellationToken)).PasswordHash);
        Assert.Null((await verificationContext.StudentSessions.SingleAsync(cancellationToken)).RevokedOn);
        Assert.Null((await verificationContext.VerificationTokens.SingleAsync(
            token => token.Purpose == "recuperacao-de-senha",
            cancellationToken)).ConsumedOn);
        Assert.Empty(await verificationContext.OutboxMessages.Where(
            message => message.RoutingKey == "identidade.senha-redefinida.v1").ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task StudentPasswordRecoveryReset_ConcurrentAttemptsConsumeTheTokenAndPublishOneFact()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        string rawToken;
        Guid accountId;
        await using (var setupContext = fixture.CreateDbContext(tenantId))
        {
            await RegisterAndGetConfirmationTokenAsync(setupContext, tenantId, "recovery-concurrent-registration", cancellationToken);
            accountId = (await setupContext.Accounts.SingleAsync(cancellationToken)).Id;
            rawToken = await RequestResetAsync(setupContext, tenantId, "recovery-concurrent-request", cancellationToken);
        }

        await using var firstContext = fixture.CreateDbContext(tenantId);
        await using var secondContext = fixture.CreateDbContext(tenantId);
        var outcomes = await Task.WhenAll(
            ResetOutcomeAsync(
                CreateResetUseCase(firstContext),
                new ResetStudentPasswordInput(tenantId, rawToken, "SenhaUm2!", "recovery-concurrent-1"),
                cancellationToken),
            ResetOutcomeAsync(
                CreateResetUseCase(secondContext),
                new ResetStudentPasswordInput(tenantId, rawToken, "SenhaDois3!", "recovery-concurrent-2"),
                cancellationToken));

        Assert.Contains("204", outcomes);
        Assert.Contains("PASSWORD_RESET_REJECTED", outcomes);
        await using var verificationContext = fixture.CreateDbContext(tenantId);
        var credential = await verificationContext.Credentials.SingleAsync(cancellationToken);
        var hasher = new Pbkdf2PasswordHasher();
        Assert.NotEqual(hasher.Verify("SenhaUm2!", credential.PasswordHash), hasher.Verify("SenhaDois3!", credential.PasswordHash));
        Assert.Equal(1, await verificationContext.OutboxMessages.CountAsync(
            message => message.RoutingKey == "identidade.senha-redefinida.v1",
            cancellationToken));
        Assert.Equal(accountId, (await verificationContext.Accounts.SingleAsync(cancellationToken)).Id);
    }

    private static async Task<string> RegisterAndGetConfirmationTokenAsync(
        IdentityDbContext dbContext,
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await CreateRegistrationUseCase(dbContext).ExecuteAsync(
            new RegisterStudentAccountInput(
                tenantId,
                "Ana Souza",
                "student@example.com",
                "SenhaForte1!",
                idempotencyKey),
            cancellationToken);
        var request = await dbContext.OutboxMessages.SingleAsync(
            message => message.RoutingKey == "notificacao.envio-solicitado.v1",
            cancellationToken);
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(request));
        return ExtractToken(payload.RootElement.GetProperty("dados").GetProperty("link").GetString()!);
    }

    private static async Task<string> RequestResetAsync(
        IdentityDbContext dbContext,
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var output = await CreateRequestUseCase(dbContext).ExecuteAsync(
            new RequestStudentPasswordResetInput(tenantId, "student@example.com", idempotencyKey),
            cancellationToken);
        Assert.Equal(202, output.StatusCode);
        var message = (await GetRecoveryMessagesAsync(dbContext, cancellationToken)).Last();
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(message));
        return ExtractToken(payload.RootElement.GetProperty("dados").GetProperty("link").GetString()!);
    }

    private static Task<List<CodeForCoders.Identity.Infra.Data.Outbox.OutboxMessage>> GetRecoveryMessagesAsync(
        IdentityDbContext dbContext,
        CancellationToken cancellationToken)
        => GetRecoveryMessagesCoreAsync(dbContext, cancellationToken);

    private static async Task<List<CodeForCoders.Identity.Infra.Data.Outbox.OutboxMessage>> GetRecoveryMessagesCoreAsync(
        IdentityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages
            .Where(message => message.RoutingKey == "notificacao.envio-solicitado.v1")
            .OrderBy(message => message.OccurredOn)
            .ToListAsync(cancellationToken);
        return messages.Where(IsRecoveryMessage).ToList();
    }

    private static bool IsRecoveryMessage(CodeForCoders.Identity.Infra.Data.Outbox.OutboxMessage message)
    {
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(message));
        return payload.RootElement.GetProperty("finalidade").GetString() == "recuperacao-de-senha";
    }

    private static RegisterStudentAccount CreateRegistrationUseCase(IdentityDbContext dbContext)
    {
        var destinations = Destinations();
        var options = RegistrationOptions();
        return new RegisterStudentAccount(
            new IdentityRegistrationStore(dbContext),
            new StudentRegistrationMessageWriter(new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector), options, destinations),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            CreateFingerprinter(),
            options,
            TimeProvider.System,
            new RegisterStudentAccountInputValidator());
    }

    private static RequestStudentPasswordReset CreateRequestUseCase(IdentityDbContext dbContext)
    {
        var destinations = Destinations();
        return new RequestStudentPasswordReset(
            new IdentityPasswordRecoveryStore(dbContext),
            new StudentPasswordRecoveryMessageWriter(new OutboxMessageWriter(dbContext, destinations, OutboxTestProtection.Protector), RegistrationOptions(), destinations),
            new IdentityUnitOfWork(dbContext),
            CreateFingerprinter(),
            RegistrationOptions(),
            TimeProvider.System,
            new RequestStudentPasswordResetInputValidator());
    }

    private static ResetStudentPassword CreateResetUseCase(IdentityDbContext dbContext)
        => new(
            new IdentityPasswordRecoveryStore(dbContext),
            new StudentPasswordRecoveryMessageWriter(new OutboxMessageWriter(dbContext, Destinations(), OutboxTestProtection.Protector), RegistrationOptions(), Destinations()),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            CreateFingerprinter(),
            TimeProvider.System,
            new ResetStudentPasswordInputValidator());

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

    private static IIdempotencyFingerprinter CreateFingerprinter()
        => new IdempotencyFingerprinter(Options.Create(new IdempotencyOptions
        {
            FingerprintKeyBase64 = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
        }));

    private static string ExtractToken(string link)
    {
        var uri = new Uri(link);
        var tokenValue = uri.Query.TrimStart('?').Split("token=", 2, StringSplitOptions.None)[1];
        return Uri.UnescapeDataString(tokenValue);
    }

    private static void AssertPayloadIsProtected(string persistedPayload, string rawToken)
    {
        using var persisted = JsonDocument.Parse(persistedPayload);
        Assert.Equal(OutboxPayloadProtector.Algorithm, persisted.RootElement.GetProperty("$protected").GetString());
        Assert.DoesNotContain(rawToken, persistedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(rawToken), persistedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("token=", persistedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("student@example.com", persistedPayload, StringComparison.OrdinalIgnoreCase);
    }

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static async Task<string> ResetOutcomeAsync(
        ResetStudentPassword useCase,
        ResetStudentPasswordInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await useCase.ExecuteAsync(input, cancellationToken);
            return result.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (StudentPasswordRecoveryException exception)
        {
            return exception.Code;
        }
    }
}
