using System.Diagnostics;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Contracts;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.Services;

public sealed class StaffInvitationMessageWriter(
    IOutboxMessageWriter outboxMessageWriter,
    IOptions<StaffInvitationOptions> invitationOptions,
    IOptions<OutboxDestinationOptions> destinationOptions) : IStaffInvitationMessageWriter
{
    private const string AuditType = "AtoPraticadoPayloadV1";
    private const string AuditRoutingKey = "auditoria.ato-praticado.v1";
    private const string InvitationPurpose = "convite-interno";
    private const string SendRequestType = "NotificationSendRequestedV1";
    private const string SendRequestRoutingKey = "notificacao.envio-solicitado.v1";

    public async Task AppendInvitationIssuedAsync(
        StaffInvitation invitation,
        string rawToken,
        Guid actorAccountId,
        string reason,
        DateTimeOffset issuedOn,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.CreateVersion7(issuedOn);
        await outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                requestId,
                invitation.TenantId,
                SendRequestType,
                SendRequestRoutingKey,
                new StaffInvitationEmailRequestedV1(
                    requestId,
                    invitation.TenantId,
                    invitation.Email,
                    InvitationPurpose,
                    InvitationPurpose,
                    new StaffInvitationEmailDataV1(invitation.OfferedRole, AddTokenToLink(rawToken)),
                    issuedOn),
                issuedOn,
                Activity.Current?.Id,
                destinationOptions.Value.NotificationExchange,
                CorrelationId(invitation.Id),
                ProtectPayload: true),
            cancellationToken);

        var factId = Guid.CreateVersion7(issuedOn.AddTicks(1));
        await outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                factId,
                invitation.TenantId,
                AuditType,
                AuditRoutingKey,
                new StaffInvitationIssuedAuditFactV1(
                    factId,
                    "identidade",
                    "convite-interno-emitido",
                    invitation.TenantId,
                    issuedOn,
                    new IdentityReferenceV1("conta-interna", actorAccountId),
                    new IdentityReferenceV1("convite-interno", invitation.Id),
                    new StaffInvitationAuditComplementV1(invitation.OfferedRole),
                    reason),
                issuedOn,
                Activity.Current?.Id,
                destinationOptions.Value.AuditExchange,
                CorrelationId(invitation.Id),
                ProtectPayload: true),
            cancellationToken);
    }

    private string AddTokenToLink(string rawToken)
    {
        var baseUrl = invitationOptions.Value.AcceptanceBaseUrl;
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
    }

    private static string CorrelationId(Guid invitationId)
        => $"identidade-convite-interno-{invitationId:D}";
}
