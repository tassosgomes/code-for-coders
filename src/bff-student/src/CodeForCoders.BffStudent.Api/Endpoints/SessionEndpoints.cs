using CodeForCoders.BffStudent.Api.ApiModels;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/bff/session", (Delegate)GetStatusAsync)
            .WithName("GetBffStudentSessionStatus")
            .WithTags("Session")
            .Produces<BffSessionStatusResponse>(StatusCodes.Status200OK);
    }

    private static Task<Ok<BffSessionStatusResponse>> GetStatusAsync(HttpContext context)
        => Task.FromResult(TypedResults.Ok(new BffSessionStatusResponse(
            BffSessionContext.Get(context) is not null)));
}
