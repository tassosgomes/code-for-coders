namespace CodeForCoders.Billing.Contracts;

public sealed record PaymentNotConfirmedV1(
    Guid EventId,
    Guid TenantId,
    Guid PaymentId,
    Guid OrderId,
    string Reason,
    DateTimeOffset OccurredAt);
