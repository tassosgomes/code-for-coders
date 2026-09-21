namespace CodeForCoders.Notification.Contracts;

public sealed record NotificationSendRequestedV1(
    Guid PedidoId,
    Guid TenantId,
    string? Destinatario,
    string? Finalidade,
    string? Modelo,
    NotificationTemplateDataV1? Dados,
    DateTimeOffset SolicitadoEm);

public sealed record NotificationTemplateDataV1(
    string? Nome,
    string? Link);

public sealed record NotificationMessageDeliveredV1(
    Guid PedidoId,
    Guid TenantId,
    string Finalidade,
    string Destinatario,
    string Canal,
    DateTimeOffset EntregueEm);
