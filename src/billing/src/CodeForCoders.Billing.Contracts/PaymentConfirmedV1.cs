namespace CodeForCoders.Billing.Contracts;

public sealed record PaymentConfirmedV1(Guid EventId, Guid TenantId, Guid PaymentId, Guid OrderId, string Method,
 int AmountCents, string Currency, string GatewayReference, DateTimeOffset ConfirmedAt, DateTimeOffset OccurredAt);
