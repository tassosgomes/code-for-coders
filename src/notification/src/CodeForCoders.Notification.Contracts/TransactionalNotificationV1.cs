using System.Text.Json.Serialization;

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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Nome,
    string? Link,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Papel = null);

public sealed record NotificationMessageDeliveredV1(
    Guid PedidoId,
    Guid TenantId,
    string Finalidade,
    string Destinatario,
    string Canal,
    DateTimeOffset EntregueEm);

public sealed record NotificationDeliveryFailedV1(
    Guid PedidoId,
    Guid TenantId,
    string Finalidade,
    string Motivo,
    bool EsgotouTentativas,
    DateTimeOffset FalhouEm);
