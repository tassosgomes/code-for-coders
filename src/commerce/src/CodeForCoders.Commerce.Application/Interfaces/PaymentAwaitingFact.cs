namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record PaymentAwaitingFact(Guid EventId, Guid TenantId, Guid PaymentId, Guid OrderId, string Method,
 DateTimeOffset ExpiresAt, string GatewayReference, DateTimeOffset OccurredAt);
