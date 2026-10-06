using System.Text.Json.Serialization;

namespace CodeForCoders.Notification.Contracts;

public sealed record NotificationSendRequestedV1(
    Guid PedidoId,
    Guid TenantId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Destinatario,
    string? Finalidade,
    string? Modelo,
    NotificationTemplateDataV1? Dados,
    DateTimeOffset SolicitadoEm,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NotificationAccountRecipientV1? DestinatarioConta = null);

public sealed record NotificationTemplateDataV1(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Nome,
    string? Link,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Papel = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NumeroPedido = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Curso = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Opcao = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ValorCentavos = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Meio = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? PagoEm = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NotificationAccessPeriodV1? Vigencia = null);

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
