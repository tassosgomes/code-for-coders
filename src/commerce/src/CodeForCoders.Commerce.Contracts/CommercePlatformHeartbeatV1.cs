namespace CodeForCoders.Commerce.Contracts;

public sealed record CommercePlatformHeartbeatV1(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredOn,
    string ServiceName);
