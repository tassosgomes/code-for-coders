namespace CodeForCoders.Identity.Contracts;

public sealed record StudentAccountCreatedV1(
    Guid EventId,
    string TenantId,
    Guid AccountId,
    DateTimeOffset OccurredAt);

public sealed record StudentAccountConfirmedV1(
    Guid EventId,
    string TenantId,
    Guid AccountId,
    DateTimeOffset OccurredAt);

public sealed record StudentAccountConfirmationTokenV1(string? Token);

public sealed record StudentAccountConfirmationEmailV1(string? Email);

public sealed record StudentAccountConfirmationRequestedV1(
    Guid PedidoId,
    string TenantId,
    string Destinatario,
    string Finalidade,
    string Modelo,
    StudentAccountConfirmationDataV1 Dados,
    DateTimeOffset SolicitadoEm);

public sealed record StudentAccountConfirmationDataV1(string Nome, string Link);
