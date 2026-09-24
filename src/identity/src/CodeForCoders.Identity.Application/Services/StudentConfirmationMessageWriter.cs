using System.Diagnostics;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Contracts;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.Services;

public sealed class StudentConfirmationMessageWriter(
    IOutboxMessageWriter outboxMessageWriter,
    IOptions<RegistrationOptions> registrationOptions,
    IOptions<OutboxDestinationOptions> destinationOptions) : IStudentConfirmationMessageWriter
{
    private const string AccountConfirmedType = "StudentAccountConfirmedV1";
    private const string AccountConfirmedRoutingKey = "identidade.conta-confirmada.v1";
    private const string SendRequestType = "NotificationSendRequestedV1";
    private const string SendRequestRoutingKey = "notificacao.envio-solicitado.v1";
    private const string ConfirmationPurpose = "confirmacao-de-conta";

    public Task AppendAccountConfirmedAsync(
        Account account,
        DateTimeOffset occurredOn,
        CancellationToken cancellationToken)
    {
        var eventId = Guid.CreateVersion7(occurredOn);
        return outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                eventId,
                account.TenantId,
                AccountConfirmedType,
                AccountConfirmedRoutingKey,
                new StudentAccountConfirmedV1(eventId, account.TenantId.ToString("D"), account.Id, occurredOn),
                occurredOn,
                Activity.Current?.Id,
                destinationOptions.Value.Exchange,
                ConfirmationCorrelationId(account)),
            cancellationToken);
    }

    public Task AppendConfirmationRequestedAsync(
        Account account,
        string rawToken,
        DateTimeOffset requestedOn,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.CreateVersion7(requestedOn);
        return outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                requestId,
                account.TenantId,
                SendRequestType,
                SendRequestRoutingKey,
                new StudentAccountConfirmationRequestedV1(
                    requestId,
                    account.TenantId.ToString("D"),
                    account.Email,
                    ConfirmationPurpose,
                    ConfirmationPurpose,
                    new StudentAccountConfirmationDataV1(account.Name, AddTokenToLink(rawToken)),
                    requestedOn),
                requestedOn,
                Activity.Current?.Id,
                destinationOptions.Value.NotificationExchange,
                ConfirmationCorrelationId(account),
                ProtectPayload: true),
            cancellationToken);
    }

    private static string ConfirmationCorrelationId(Account account)
        => $"identidade-conta-{account.Id:D}";

    private string AddTokenToLink(string rawToken)
    {
        var baseUrl = registrationOptions.Value.ConfirmationBaseUrl;
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
    }
}
