using CodeForCoders.BffAdmin.Application.UseCases.Platform.RecordPlatformHeartbeat;

namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record PlatformHeartbeatResponse(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredOn,
    string RoutingKey)
{
    public static PlatformHeartbeatResponse FromOutput(RecordPlatformHeartbeatOutput output)
        => new(output.EventId, output.TenantId, output.OccurredOn, output.RoutingKey);
}
