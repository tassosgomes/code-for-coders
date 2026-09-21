namespace CodeForCoders.Notification.Contracts;

public sealed record PlatformHeartbeatV1(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredOn,
    string ServiceName);
