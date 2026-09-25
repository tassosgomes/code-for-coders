using System.Diagnostics;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Contracts;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.Services;

public sealed class StaffRoleMessageWriter(
    IOutboxMessageWriter outboxMessageWriter,
    IOptions<OutboxDestinationOptions> destinationOptions) : IStaffRoleMessageWriter
{
    private const string AuditType = "AtoPraticadoPayloadV1";
    private const string AuditRoutingKey = "auditoria.ato-praticado.v1";

    public Task AppendRoleGrantedAsync(
        Guid tenantId,
        Guid actorAccountId,
        Guid targetAccountId,
        string role,
        string reason,
        DateTimeOffset practicedOn,
        CancellationToken cancellationToken)
        => AppendAsync(
            tenantId,
            actorAccountId,
            targetAccountId,
            role,
            reason,
            "papel-concedido",
            practicedOn,
            cancellationToken);

    public Task AppendRoleRevokedAsync(
        Guid tenantId,
        Guid actorAccountId,
        Guid targetAccountId,
        string role,
        string reason,
        DateTimeOffset practicedOn,
        CancellationToken cancellationToken)
        => AppendAsync(
            tenantId,
            actorAccountId,
            targetAccountId,
            role,
            reason,
            "papel-revogado",
            practicedOn,
            cancellationToken);

    private Task AppendAsync(
        Guid tenantId,
        Guid actorAccountId,
        Guid targetAccountId,
        string role,
        string reason,
        string actType,
        DateTimeOffset practicedOn,
        CancellationToken cancellationToken)
    {
        var factId = Guid.CreateVersion7(practicedOn);
        return outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                factId,
                tenantId,
                AuditType,
                AuditRoutingKey,
                new StaffRoleAuditFactV1(
                    factId,
                    "identidade",
                    actType,
                    tenantId,
                    practicedOn,
                    new IdentityReferenceV1("conta-interna", actorAccountId),
                    new IdentityReferenceV1("conta-interna", targetAccountId),
                    new StaffRoleAuditComplementV1(role),
                    reason),
                practicedOn,
                Activity.Current?.Id,
                destinationOptions.Value.AuditExchange,
                $"identidade-papel-{factId:D}",
                ProtectPayload: true),
            cancellationToken);
    }
}
