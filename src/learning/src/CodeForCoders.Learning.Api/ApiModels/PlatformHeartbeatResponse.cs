using CodeForCoders.Learning.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.Learning.Api.ApiModels;

public sealed record PlatformHeartbeatResponse(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredOn,
    string RoutingKey)
{
    public static PlatformHeartbeatResponse FromOutput(RecordPlatformHeartbeatOutput output)
        => new(output.EventId, output.TenantId, output.OccurredOn, output.RoutingKey);
}
