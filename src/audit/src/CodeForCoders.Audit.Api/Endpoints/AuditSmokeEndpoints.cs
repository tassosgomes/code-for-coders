using CodeForCoders.Audit.Api.ApiModels;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CodeForCoders.Audit.Api.Endpoints;

public static class AuditSmokeEndpoints
{
    public static void MapAuditSmokeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/audit/smoke", GetSmokeStatus)
            .WithName("AuditSmoke")
            .WithTags("Audit")
            .Produces<AuditSmokeResponse>(StatusCodes.Status200OK);
    }

    private static Ok<AuditSmokeResponse> GetSmokeStatus()
        => TypedResults.Ok(new AuditSmokeResponse("audit", "append-only-event-consumer"));
}
