namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record PaymentConfirmedFact(Guid EventId, Guid TenantId, Guid PaymentId, Guid OrderId, string Method,
 int AmountCents, string Currency, string GatewayReference, DateTimeOffset ConfirmedAt, DateTimeOffset OccurredAt);
