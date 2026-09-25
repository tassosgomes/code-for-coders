namespace CodeForCoders.Identity.Contracts;

public sealed record StaffInvitationEmailRequestedV1(
    Guid PedidoId,
    Guid TenantId,
    string Destinatario,
    string Finalidade,
    string Modelo,
    StaffInvitationEmailDataV1 Dados,
    DateTimeOffset SolicitadoEm);

public sealed record StaffInvitationEmailDataV1(string Papel, string Link);

public sealed record StaffInvitationIssuedAuditFactV1(
    Guid FatoId,
    string Origem,
    string Tipo,
    Guid TenantId,
    DateTimeOffset PraticadoEm,
    IdentityReferenceV1 Autor,
    IdentityReferenceV1 Alvo,
    StaffInvitationAuditComplementV1 Complemento,
    string Motivo);

public sealed record IdentityReferenceV1(string Tipo, Guid Id);

public sealed record StaffInvitationAuditComplementV1(string Papel);
