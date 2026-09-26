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

public sealed record StaffInvitationAcceptedAuditFactV1(
    Guid FatoId,
    string Origem,
    string Tipo,
    Guid TenantId,
    DateTimeOffset PraticadoEm,
    IdentityReferenceV1 Autor,
    IdentityReferenceV1 Alvo);

public sealed record IdentityReferenceV1(string Tipo, Guid Id);

public sealed record StaffInvitationAuditComplementV1(string Papel);

public sealed record StaffRoleAuditFactV1(
    Guid FatoId,
    string Origem,
    string Tipo,
    Guid TenantId,
    DateTimeOffset PraticadoEm,
    IdentityReferenceV1 Autor,
    IdentityReferenceV1 Alvo,
    StaffRoleAuditComplementV1 Complemento,
    string Motivo);

public sealed record StaffRoleAuditComplementV1(string Papel);
