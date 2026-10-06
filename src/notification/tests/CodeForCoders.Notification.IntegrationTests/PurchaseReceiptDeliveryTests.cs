using System.Net;
using CodeForCoders.Notification.Api.Clients;
using CodeForCoders.Notification.Application.Exceptions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Net.Http.Json;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Contracts;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Notification.IntegrationTests;

[Collection(NotificationIntegrationCollection.Name)]
public sealed class PurchaseReceiptDeliveryTests(NotificationIntegrationFixture fixture)
{
    [Fact(DisplayName = nameof(ReceiptResolvesContactAtDeliveryAndRendersFrozenTerms))]
    public async Task ReceiptResolvesContactAtDeliveryAndRendersFrozenTerms()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        await h.StartAsync();
        var request = h.Request();
        await h.PublishAsync(request);
        var record = await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Delivered);
        var email = Assert.Single(h.Sender.Emails);
        Assert.Equal(h.Email, email.To);
        Assert.Equal(h.StudentId, record.RecipientAccountId);
        Assert.NotNull(record.ReceiptData);
        foreach (var text in new[] { "Oi, Ana!", "000123", ".NET <Avançado>", "12 meses", "497,00", "cartão de crédito", "05/10/2026 11:21", "acesso por 12 meses a partir da liberação", "não é documento fiscal", request.Dados!.Link! })
            Assert.Contains(text, email.TextBody, StringComparison.Ordinal);
        Assert.Contains(".NET &lt;Avan", email.HtmlBody!);
        Assert.DoesNotContain(".NET <Avançado>", email.HtmlBody!);
        Assert.Contains(".NET <Avançado>", WebUtility.HtmlDecode(email.HtmlBody!));
        Assert.All(h.Logs, line => Assert.DoesNotContain(h.Email, line, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Ver meu pedido", email.HtmlBody!);
        Assert.Contains("display:inline-block;padding:12px 20px", email.HtmlBody!);
        await h.AssertEventAsync("notificacao.mensagem-entregue.v1", request.PedidoId,
            json => Assert.Equal(h.Email, json.GetProperty("destinatario").GetString()));
        h.AssertAssertions();
    }

    [Fact(DisplayName = nameof(DisabledAccountFailsWithoutExhaustingAttempts))]
    public async Task DisabledAccountFailsWithoutExhaustingAttempts()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        h.Contacts.Response = _ => new(HttpStatusCode.OK)
        { Content = JsonContent.Create(new StudentContact(h.StudentId, h.Email, "Ana", "disabled")) };
        await UnavailableAsync(h);
    }

    [Fact(DisplayName = nameof(MissingAccountFailsWithoutDelivery))]
    public async Task MissingAccountFailsWithoutDelivery()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        h.Contacts.Response = _ => new(HttpStatusCode.NotFound);
        await UnavailableAsync(h);
    }

    [Fact(DisplayName = nameof(IdentityFailureIsRetainedAndDeliveredWhenIdentityReturns))]
    public async Task IdentityFailureIsRetainedAndDeliveredWhenIdentityReturns()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        var success = h.Contacts.Response;
        h.Contacts.Response = _ => new(HttpStatusCode.BadRequest);
        await h.StartAsync();
        var request = h.Request();
        await h.PublishAsync(request);
        var pending = await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Accepted && r.ProviderAttemptCount > 0);
        Assert.NotNull(pending.NextAttemptOn);
        Assert.Equal("identity-indisponivel", pending.Reason);
        Assert.Empty(h.Sender.Emails);
        h.Contacts.Response = success;
        var delivered = await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Delivered);
        Assert.True(delivered.ProviderAttemptCount >= 2);
        Assert.Single(h.Sender.Emails);
        h.AssertAssertions();
    }

    [Fact(DisplayName = nameof(BothOrNeitherRecipientsAreRefusedAsInvalidFormat))]
    public async Task BothOrNeitherRecipientsAreRefusedAsInvalidFormat()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        await h.StartAsync();
        foreach (var request in new[] { h.Request() with { Destinatario = h.Email }, h.Request() with { DestinatarioConta = null } })
        {
            await h.PublishAsync(request);
            var refused = await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Refused);
            Assert.Equal("forma inválida", refused.Reason);
        }
        Assert.Empty(h.Sender.Emails);
        Assert.Empty(h.Contacts.Tokens);
    }

    [Fact(DisplayName = nameof(ReceiptWithEmailAndNoAccountIsRefused))]
    public async Task ReceiptWithEmailAndNoAccountIsRefused()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        await h.StartAsync();
        var request = h.Request() with { DestinatarioConta = null, Destinatario = h.Email };
        await h.PublishAsync(request);
        var refused = await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Refused);
        Assert.Equal("forma inválida", refused.Reason);
        Assert.Empty(h.Sender.Emails);
    }

    [Fact(DisplayName = nameof(RedeliveryProducesOnlyOneEmail))]
    public async Task RedeliveryProducesOnlyOneEmail()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        await h.StartAsync();
        var request = h.Request();
        await h.PublishAsync(request);
        await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Delivered);
        await h.PublishAsync(request);
        await using var scope = h.Host.Services.CreateAsyncScope();
        var accept = scope.ServiceProvider.GetRequiredService<CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest.IAcceptNotificationSendRequest>();
        var repeated = await accept.ExecuteAsync(new(request, "receipt-proof"), TestContext.Current.CancellationToken);
        Assert.Equal(DeliveryStatus.Delivered, repeated.Status);
        Assert.Single(h.Sender.Emails);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().DeliveryRecords.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(LegacyEmailRequestContinuesToDeliverWithoutContactLookup))]
    public async Task LegacyEmailRequestContinuesToDeliverWithoutContactLookup()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        await h.StartAsync();
        var request = new NotificationSendRequestedV1(Guid.CreateVersion7(), h.TenantId, h.Email,
            "confirmacao-de-conta", "confirmacao-de-conta", new("Ana", "https://identity.test/confirm"), DateTimeOffset.UtcNow);
        await h.PublishAsync(request);
        await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Delivered);
        Assert.Equal("Confirme seu cadastro na Code4Coders", Assert.Single(h.Sender.Emails).Subject);
        Assert.Empty(h.Contacts.Tokens);
    }

    [Fact(DisplayName = nameof(PixAndBoletoLifetimeReceiptsRenderPaymentMethodAndPeriod))]
    public async Task PixAndBoletoLifetimeReceiptsRenderPaymentMethodAndPeriod()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        await h.StartAsync();
        foreach (var (method, label) in new[] { ("pix", "PIX"), ("boleto", "boleto") })
        {
            var request = h.Request();
            request = request with { Dados = request.Dados! with { Meio = method, Vigencia = new("lifetime") } };
            await h.PublishAsync(request);
            await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Delivered);
            var email = h.Sender.Emails.Last();
            Assert.Contains(label, email.TextBody);
            Assert.Contains("acesso vitalício", email.TextBody);
        }
    }

    [Fact(DisplayName = nameof(HttpRetrySignsANewAssertionForEveryAttempt))]
    public async Task HttpRetrySignsANewAssertionForEveryAttempt()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        var success = h.Contacts.Response;
        var count = 0;
        h.Contacts.Response = request => Interlocked.Increment(ref count) == 1 ? new(HttpStatusCode.ServiceUnavailable) : success(request);
        await h.StartAsync();
        var request = h.Request();
        await h.PublishAsync(request);
        await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Delivered);
        Assert.Equal(2, h.Contacts.Tokens.Count);
        h.AssertAssertions();
        Assert.Single(h.Sender.Emails);
    }

    [Fact(DisplayName = nameof(InvalidContactResponseIsRetainedWithoutSendingOrLoggingPersonalData))]
    public async Task InvalidContactResponseIsRetainedWithoutSendingOrLoggingPersonalData()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        h.Contacts.Response = _ => new(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { studentId = h.StudentId, email = h.Email, name = "Ana", status = "active", extra = "invalid" }) };
        await h.StartAsync();
        var request = h.Request();
        await h.PublishAsync(request);
        await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Accepted && r.ProviderAttemptCount > 0);
        Assert.Empty(h.Sender.Emails);
        Assert.All(h.Logs, line => Assert.DoesNotContain(h.Email, line, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = nameof(IdentityOutageExhaustsTheSameDeliveryPolicyAsProviderOutage))]
    public async Task IdentityOutageExhaustsTheSameDeliveryPolicyAsProviderOutage()
    {
        await using var h = new PurchaseReceiptHost(fixture);
        h.Contacts.Response = _ => new(HttpStatusCode.BadRequest);
        await h.StartAsync();
        var request = h.Request();
        await h.PublishAsync(request);
        var failed = await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Failed);
        Assert.True(failed.ExhaustedAttempts);
        Assert.Equal(3, failed.ProviderAttemptCount);
        Assert.Empty(h.Sender.Emails);
        await h.AssertEventAsync("notificacao.entrega-falhou.v1", request.PedidoId,
            json => Assert.True(json.GetProperty("esgotouTentativas").GetBoolean()));
    }

    [Fact(DisplayName = nameof(ResilienceTimeoutAndCircuitRejectionMapToTransientIdentityFailure))]
    public async Task ResilienceTimeoutAndCircuitRejectionMapToTransientIdentityFailure()
    {
        foreach (var failure in new ExecutionRejectedException[] { new TimeoutRejectedException(), new BrokenCircuitException() })
        {
            using var handler = new ReceiptContactHandler { Response = _ => throw failure };
            using var http = new HttpClient(handler) { BaseAddress = new Uri("http://identity.test/") };
            var client = new StudentContactClient(http);
            var error = await Assert.ThrowsAsync<TransactionalEmailSendException>(() =>
                client.GetAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), TestContext.Current.CancellationToken));
            Assert.True(error.IsTransient);
            Assert.Equal("identity-indisponivel", error.Reason);
        }
    }

    private static async Task UnavailableAsync(PurchaseReceiptHost h)
    {
        await h.StartAsync();
        var request = h.Request();
        await h.PublishAsync(request);
        var failed = await h.WaitAsync(request.PedidoId, r => r.Status == DeliveryStatus.Failed);
        Assert.Equal("destinatario-indisponivel", failed.Reason);
        Assert.False(failed.ExhaustedAttempts);
        Assert.Empty(h.Sender.Emails);
        await h.AssertEventAsync("notificacao.entrega-falhou.v1", request.PedidoId, json =>
        {
            Assert.Equal("destinatario-indisponivel", json.GetProperty("motivo").GetString());
            Assert.False(json.GetProperty("esgotouTentativas").GetBoolean());
        });
    }
}
