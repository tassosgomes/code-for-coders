using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.CourtesyCourses.ListCourtesyCourses;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class CourtesyCourseEndpoints
{
    public static void MapCourtesyCourseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/v1/courtesy-courses", ListAsync)
            .RequireAuthorization(CourtesyPolicies.Grant).WithTags("Courtesies").WithName("listCourtesyCoursesInternal");
    }

    private static async Task<IResult> ListAsync(HttpContext context, ITenantContext tenantContext,
        IListCourtesyCourses useCase, CancellationToken cancellationToken, int _page = 1, int _size = 10, string? title = null)
    {
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty)
            return Results.Problem(statusCode: 401, title: "Access token tenant is invalid.",
                extensions: new Dictionary<string, object?> { ["code"] = "TOKEN_INVALID" });
        tenantContext.Set(tenantId);
        return Results.Ok(await useCase.ExecuteAsync(new(_page, _size, title), cancellationToken));
    }
}
