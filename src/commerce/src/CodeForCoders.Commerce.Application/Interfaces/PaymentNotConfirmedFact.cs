namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record PaymentNotConfirmedFact(
    Guid EventId,
    Guid TenantId,
    Guid PaymentId,
    Guid OrderId,
    string Reason,
    DateTimeOffset OccurredAt);
