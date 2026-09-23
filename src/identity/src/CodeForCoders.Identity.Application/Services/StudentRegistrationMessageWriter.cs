using System.Diagnostics;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Contracts;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.Services;

public sealed class StudentRegistrationMessageWriter(
    IOutboxMessageWriter outboxMessageWriter,
    IOptions<RegistrationOptions> registrationOptions,
    IOptions<OutboxDestinationOptions> destinationOptions) : IStudentRegistrationMessageWriter
{
    private const string AccountCreatedType = "StudentAccountCreatedV1";
    private const string AccountCreatedRoutingKey = "identidade.conta-criada.v1";
    private const string SendRequestType = "NotificationSendRequestedV1";
    private const string SendRequestRoutingKey = "notificacao.envio-solicitado.v1";
    private const string ConfirmationPurpose = "confirmacao-de-conta";

    public async Task AppendAsync(
        Account account,
        string confirmationToken,
        DateTimeOffset occurredOn,
        CancellationToken cancellationToken)
    {
        var eventId = Guid.CreateVersion7(occurredOn);
        var requestId = Guid.CreateVersion7(occurredOn.AddTicks(1));
        var tenantId = account.TenantId.ToString("D");
        var correlationId = RegistrationCorrelationId(account);
        await outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                eventId,
                account.TenantId,
                AccountCreatedType,
                AccountCreatedRoutingKey,
                new StudentAccountCreatedV1(eventId, tenantId, account.Id, occurredOn),
                occurredOn,
                Activity.Current?.Id,
                destinationOptions.Value.Exchange,
                correlationId),
            cancellationToken);
        await outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                requestId,
                account.TenantId,
                SendRequestType,
                SendRequestRoutingKey,
                new StudentAccountConfirmationRequestedV1(
                    requestId,
                    tenantId,
                    account.Email,
                    ConfirmationPurpose,
                    ConfirmationPurpose,
                    new StudentAccountConfirmationDataV1(account.Name, AddTokenToLink(confirmationToken)),
                    occurredOn),
                occurredOn,
                Activity.Current?.Id,
                destinationOptions.Value.NotificationExchange,
                correlationId),
            cancellationToken);
    }

    public static string RegistrationCorrelationId(Account account)
    {
        return $"identidade-cadastro-{account.Id:D}";
    }

    private string AddTokenToLink(string rawToken)
    {
        var baseUrl = registrationOptions.Value.ConfirmationBaseUrl;
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
    }
}
