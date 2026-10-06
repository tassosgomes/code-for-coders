namespace CodeForCoders.Billing.Contracts;

public sealed record OrderCancelledV1(
    Guid EventId,
    Guid TenantId,
    Guid OrderId,
    Guid StudentId,
    DateTimeOffset OccurredAt);
