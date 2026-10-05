namespace CodeForCoders.Billing.Application.Interfaces;

public sealed record GatewayEvent(string Id, string Type, DateTimeOffset OccurredAt, string ObjectReference,
 string? SessionReference, string? PaymentReference, Guid? TenantId, Guid? OrderId, string? Outcome,
 string? Method, int? AmountCents, string? Currency);
