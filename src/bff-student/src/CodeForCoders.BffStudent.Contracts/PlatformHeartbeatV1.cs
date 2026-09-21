namespace CodeForCoders.BffStudent.Contracts;

public sealed record PlatformHeartbeatV1(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredOn,
    string Source);
