using System.Diagnostics;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Contracts;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.Services;

public sealed class StaffPasswordRecoveryMessageWriter(
    IOutboxMessageWriter outboxMessageWriter,
    IOptions<StaffAccountOptions> staffAccountOptions,
    IOptions<OutboxDestinationOptions> destinationOptions) : IStaffPasswordRecoveryMessageWriter
{
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
                "NotificationSendRequestedV1",
                "notificacao.envio-solicitado.v1",
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
                $"identidade-recuperacao-senha-{account.Id:D}",
                ProtectPayload: true),
            cancellationToken);
    }

    private string AddTokenToLink(string rawToken)
    {
        var baseUrl = staffAccountOptions.Value.PasswordResetBaseUrl;
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
    }

    private const string PasswordRecoveryPurpose = "recuperacao-de-senha";
}
