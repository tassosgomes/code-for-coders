using CodeForCoders.Billing.Application.Interfaces;
namespace CodeForCoders.Billing.Infra.Data.Payments;

public sealed class GatewayInboxEntry
{
    private GatewayInboxEntry() { }
    public string Id { get; private set; } = "";
    public string Namespace { get; private set; } = "";
    public string Type { get; private set; } = "";
    public DateTimeOffset OccurredAt { get; private set; }
    public string ObjectReference { get; private set; } = "";
    public string? SessionReference { get; private set; }
    public string? PaymentReference { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? OrderId { get; private set; }
    public string? Outcome { get; private set; }
    public string? Method { get; private set; }
    public int? AmountCents { get; private set; }
    public string? Currency { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public static GatewayInboxEntry Create(GatewayEvent value, string processingNamespace) => new()
    {
        Id = value.Id,
        Namespace = processingNamespace,
        Type = value.Type,
        OccurredAt = value.OccurredAt,
        ObjectReference = value.ObjectReference,
        SessionReference = value.SessionReference,
        PaymentReference = value.PaymentReference,
        TenantId = value.TenantId,
        OrderId = value.OrderId,
        Outcome = value.Outcome,
        Method = value.Method,
        AmountCents = value.AmountCents,
        Currency = value.Currency,
        Reason = value.Reason
    };
    public void MarkProcessed(DateTimeOffset now) => ProcessedAt = now;
}
