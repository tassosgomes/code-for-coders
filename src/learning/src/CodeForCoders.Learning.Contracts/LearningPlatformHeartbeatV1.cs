namespace CodeForCoders.Learning.Contracts;

public sealed record LearningPlatformHeartbeatV1(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredOn,
    string ServiceName);
