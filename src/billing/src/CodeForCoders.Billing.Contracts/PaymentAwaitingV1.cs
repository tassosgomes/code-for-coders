namespace CodeForCoders.Billing.Contracts;

public sealed record PaymentAwaitingV1(Guid EventId, Guid TenantId, Guid PaymentId, Guid OrderId, string Method,
 DateTimeOffset ExpiresAt, string GatewayReference, DateTimeOffset OccurredAt);
