using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;
using CodeForCoders.Identity.Application.Services;
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
public sealed class StudentRegistrationTests(IdentityIntegrationFixture fixture)
{
    [Fact]
    public async Task StudentRegistration_CreatesUnconfirmedAccountCredentialTokenAndOutboxMessages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateUseCase(dbContext);

        var output = await useCase.ExecuteAsync(
            NewInput(tenantId, "registration-1"),
            cancellationToken);

        Assert.Equal(202, output.StatusCode);
        var account = await dbContext.Accounts.SingleAsync(cancellationToken);
        Assert.Equal("student@example.com", account.NormalizedEmail);
        Assert.Equal("student@example.com", account.Email);
        Assert.False(account.IsConfirmed);
        Assert.Equal(AccountType.Student, account.Type);

        var credential = await dbContext.Credentials.SingleAsync(cancellationToken);
        Assert.NotEqual("SenhaForte1!", credential.PasswordHash);
        Assert.StartsWith("pbkdf2-sha256$", credential.PasswordHash);
        var token = await dbContext.VerificationTokens.SingleAsync(cancellationToken);
        Assert.Equal("confirmacao-de-conta", token.Purpose);
        Assert.Equal(64, token.TokenHash.Length);

        var messages = await dbContext.OutboxMessages.ToListAsync(cancellationToken);
        Assert.Equal(2, messages.Count);
        var fact = Assert.Single(messages, message => message.RoutingKey == "identidade.conta-criada.v1");
        Assert.Equal("identity.events", fact.Exchange);
        Assert.DoesNotContain("student@example.com", fact.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SenhaForte1!", fact.Payload, StringComparison.Ordinal);

        var sendRequest = Assert.Single(messages, message => message.RoutingKey == "notificacao.envio-solicitado.v1");
        Assert.Equal("notification.events.default", sendRequest.Exchange);
        Assert.DoesNotContain("student@example.com", sendRequest.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=", sendRequest.Payload, StringComparison.Ordinal);
        using var payload = JsonDocument.Parse(OutboxTestProtection.ReadPayload(sendRequest));
        Assert.Equal("student@example.com", payload.RootElement.GetProperty("destinatario").GetString());
        Assert.Contains("token=", payload.RootElement.GetProperty("dados").GetProperty("link").GetString(), StringComparison.Ordinal);

        var expectedCorrelationId = $"identidade-cadastro-{account.Id:D}";
        Assert.Equal(expectedCorrelationId, fact.CorrelationId);
        Assert.Equal(expectedCorrelationId, sendRequest.CorrelationId);
    }

    [Fact]
    public async Task StudentRegistration_PublishesFactAndSendRequestWithContractCorrelationId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        await CreateUseCase(dbContext).ExecuteAsync(NewInput(tenantId, "registration-6"), cancellationToken);
        var account = await dbContext.Accounts.SingleAsync(cancellationToken);
        var messages = await dbContext.OutboxMessages.ToListAsync(cancellationToken);

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
        foreach (var message in messages)
        {
            await channel.ExchangeDeclareAsync(
                message.Exchange,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(queue.QueueName, message.Exchange, message.RoutingKey, cancellationToken: cancellationToken);
        }

        var publisher = new RabbitMqPublisher(connectionProvider, OutboxTestProtection.Protector);
        foreach (var message in messages)
        {
            await publisher.PublishAsync(message, cancellationToken);
        }

        var expectedCorrelationId = $"identidade-cadastro-{account.Id:D}";
        var received = new List<string>();
        for (var index = 0; index < messages.Count; index++)
        {
            var delivery = await channel.BasicGetAsync(queue.QueueName, autoAck: true, cancellationToken);
            Assert.NotNull(delivery);
            Assert.Equal(expectedCorrelationId, delivery.BasicProperties.CorrelationId);
            Assert.NotNull(delivery.BasicProperties.Headers);
            var header = Assert.IsType<byte[]>(delivery.BasicProperties.Headers["correlationId"]);
            Assert.Equal(expectedCorrelationId, Encoding.UTF8.GetString(header));
            Assert.DoesNotContain("student@example.com", expectedCorrelationId, StringComparison.OrdinalIgnoreCase);
            received.Add(delivery.RoutingKey);
        }

        Assert.Contains("identidade.conta-criada.v1", received);
        Assert.Contains("notificacao.envio-solicitado.v1", received);
    }

    [Fact]
    public async Task StudentRegistration_NormalizedDuplicateDoesNotCreateAnotherCredentialOrMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateUseCase(dbContext);
        await useCase.ExecuteAsync(NewInput(tenantId, "registration-2"), cancellationToken);

        var exception = await Assert.ThrowsAsync<StudentRegistrationException>(() =>
            useCase.ExecuteAsync(
                new RegisterStudentAccountInput(
                    tenantId,
                    "Another Name",
                    "STUDENT@EXAMPLE.COM",
                    "OutraSenha1!",
                    "registration-2b"),
                cancellationToken));

        Assert.Equal("ACCOUNT_ALREADY_EXISTS", exception.Code);
        Assert.Equal(1, await dbContext.Accounts.CountAsync(cancellationToken));
        Assert.Equal(1, await dbContext.Credentials.CountAsync(cancellationToken));
        Assert.Equal(1, await dbContext.VerificationTokens.CountAsync(cancellationToken));
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task StudentRegistration_ReplaysSameIntentAndRejectsChangedBodyForTheSameKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateUseCase(dbContext);
        var input = NewInput(tenantId, "registration-3");

        var first = await useCase.ExecuteAsync(input, cancellationToken);
        var replay = await useCase.ExecuteAsync(input, cancellationToken);
        var exception = await Assert.ThrowsAsync<StudentRegistrationException>(() =>
            useCase.ExecuteAsync(input with { Name = "Different Name" }, cancellationToken));

        Assert.Equal(202, first.StatusCode);
        Assert.Equal(202, replay.StatusCode);
        Assert.Equal("IDEMPOTENCY_CONFLICT", exception.Code);
        Assert.Equal(1, await dbContext.Accounts.CountAsync(cancellationToken));
        Assert.Equal(1, await dbContext.Credentials.CountAsync(cancellationToken));
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task StudentRegistration_RejectsPasswordOutsidePolicyWithoutCreatingAccountOrOutbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var dbContext = fixture.CreateDbContext(tenantId);
        var useCase = CreateUseCase(dbContext);

        var exception = await Assert.ThrowsAsync<StudentRegistrationException>(() =>
            useCase.ExecuteAsync(
                new RegisterStudentAccountInput(
                    tenantId,
                    "Ana Souza",
                    "student@example.com",
                    "abcdefgh",
                    "registration-4"),
                cancellationToken));

        Assert.Equal("PASSWORD_POLICY_VIOLATION", exception.Code);
        Assert.Empty(await dbContext.Accounts.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.Credentials.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.VerificationTokens.ToListAsync(cancellationToken));
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task StudentRegistration_ConcurrentNormalizedEmailCreatesOnlyOneAccountAndRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await using var firstContext = fixture.CreateDbContext(tenantId);
        await using var secondContext = fixture.CreateDbContext(tenantId);
        var first = CreateUseCase(firstContext);
        var second = CreateUseCase(secondContext);
        var outcomes = await Task.WhenAll(
            RegisterOutcomeAsync(first, NewInput(tenantId, "registration-5a"), cancellationToken),
            RegisterOutcomeAsync(second, NewInput(tenantId, "registration-5b") with { Email = "STUDENT@EXAMPLE.COM" }, cancellationToken));

        await using var verificationContext = fixture.CreateDbContext(tenantId);
        Assert.Contains("accepted", outcomes);
        Assert.Contains("ACCOUNT_ALREADY_EXISTS", outcomes);
        Assert.Equal(1, await verificationContext.Accounts.CountAsync(cancellationToken));
        Assert.Equal(1, await verificationContext.Credentials.CountAsync(cancellationToken));
        Assert.Equal(2, await verificationContext.OutboxMessages.CountAsync(cancellationToken));
    }

    private static async Task<string> RegisterOutcomeAsync(
        IRegisterStudentAccount useCase,
        RegisterStudentAccountInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await useCase.ExecuteAsync(input, cancellationToken);
            return result.StatusCode == 202 ? "accepted" : result.Code ?? "unexpected";
        }
        catch (StudentRegistrationException exception)
        {
            return exception.Code;
        }
    }

    private static RegisterStudentAccountInput NewInput(Guid tenantId, string idempotencyKey)
        => new(tenantId, "Ana Souza", "student@example.com", "SenhaForte1!", idempotencyKey);

    private static RegisterStudentAccount CreateUseCase(IdentityDbContext dbContext)
    {
        var key = Convert.ToBase64String(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        var idempotencyOptions = Options.Create(new IdempotencyOptions { FingerprintKeyBase64 = key });
        var destinationOptions = Options.Create(new OutboxDestinationOptions
        {
            Exchange = "identity.events",
            NotificationExchange = "notification.events.default",
        });
        return new RegisterStudentAccount(
            new IdentityRegistrationStore(dbContext),
            new StudentRegistrationMessageWriter(
                new OutboxMessageWriter(dbContext, destinationOptions, OutboxTestProtection.Protector),
                Options.Create(new RegistrationOptions
                {
                    ConfirmationBaseUrl = "https://students.example.test/confirm-account",
                    ConfirmationLifetimeHours = 24,
                }),
                destinationOptions),
            new IdentityUnitOfWork(dbContext),
            new Pbkdf2PasswordHasher(),
            new IdempotencyFingerprinter(idempotencyOptions),
            Options.Create(new RegistrationOptions
            {
                ConfirmationBaseUrl = "https://students.example.test/confirm-account",
                ConfirmationLifetimeHours = 24,
            }),
            TimeProvider.System,
            new RegisterStudentAccountInputValidator());
    }
}
