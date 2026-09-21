using CodeForCoders.Notification.Api.ApiModels;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.UseCases.Platform.RecordPlatformHeartbeat;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CodeForCoders.Notification.Api.Endpoints;

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/platform/heartbeat", RecordHeartbeatAsync)
            .WithName("RecordPlatformHeartbeat")
            .WithTags("Platform")
            .Accepts<object>("application/json")
            .Produces<PlatformHeartbeatResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<Accepted<PlatformHeartbeatResponse>> RecordHeartbeatAsync(
        HttpContext httpContext,
        IRecordPlatformHeartbeat useCase,
        ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var tenantHeader = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!Guid.TryParse(tenantHeader, out var tenantId) || tenantId == Guid.Empty)
        {
            throw new ValidationException(
                new[] { new ValidationFailure("X-Tenant-Id", "A valid tenant id is required.") });
        }

        tenantContext.Set(tenantId);
        var output = await useCase.ExecuteAsync(
            new RecordPlatformHeartbeatInput(tenantId),
            cancellationToken);
        var response = PlatformHeartbeatResponse.FromOutput(output);
        return TypedResults.Accepted($"/internal/platform/heartbeats/{response.EventId}", response);
    }
}
