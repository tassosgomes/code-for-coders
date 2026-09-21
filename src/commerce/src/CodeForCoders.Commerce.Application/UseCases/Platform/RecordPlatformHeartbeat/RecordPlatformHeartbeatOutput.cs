namespace CodeForCoders.Commerce.Application.UseCases.Platform.RecordPlatformHeartbeat;

public sealed record RecordPlatformHeartbeatOutput(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredOn,
    string RoutingKey);
