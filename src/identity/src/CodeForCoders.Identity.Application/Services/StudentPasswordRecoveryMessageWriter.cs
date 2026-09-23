using System.Diagnostics;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Contracts;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.Services;

public sealed class StudentPasswordRecoveryMessageWriter(
    IOutboxMessageWriter outboxMessageWriter,
    IOptions<RegistrationOptions> registrationOptions,
    IOptions<OutboxDestinationOptions> destinationOptions) : IStudentPasswordRecoveryMessageWriter
{
    private const string PasswordResetType = "StudentPasswordResetV1";
    private const string PasswordResetRoutingKey = "identidade.senha-redefinida.v1";
    private const string SendRequestType = "NotificationSendRequestedV1";
    private const string SendRequestRoutingKey = "notificacao.envio-solicitado.v1";
    private const string PasswordRecoveryPurpose = "recuperacao-de-senha";

    public Task AppendPasswordResetRequestedAsync(
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
                    PasswordRecoveryPurpose,
                    PasswordRecoveryPurpose,
                    new StudentAccountConfirmationDataV1(account.Name, AddTokenToLink(rawToken)),
                    requestedOn),
                requestedOn,
                Activity.Current?.Id,
                destinationOptions.Value.NotificationExchange,
                CorrelationId(account),
                ProtectPayload: true),
            cancellationToken);
    }

    public Task AppendPasswordResetAsync(
        Account account,
        DateTimeOffset resetOn,
        CancellationToken cancellationToken)
    {
        var eventId = Guid.CreateVersion7(resetOn);
        return outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                eventId,
                account.TenantId,
                PasswordResetType,
                PasswordResetRoutingKey,
                new StudentPasswordResetV1(eventId, account.TenantId.ToString("D"), account.Id, resetOn),
                resetOn,
                Activity.Current?.Id,
                destinationOptions.Value.Exchange,
                CorrelationId(account)),
            cancellationToken);
    }

    private string AddTokenToLink(string rawToken)
    {
        var baseUrl = registrationOptions.Value.PasswordResetBaseUrl;
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
    }

    private static string CorrelationId(Account account)
        => $"identidade-recuperacao-senha-{account.Id:D}";
}
