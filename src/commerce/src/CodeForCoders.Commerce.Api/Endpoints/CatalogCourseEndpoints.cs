using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.CatalogCourses.ListCatalogCourses;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class CatalogCourseEndpoints
{
    public static void MapCatalogCourseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/internal/v1/catalog").RequireAuthorization(CatalogPolicies.EditOffers)
            .MapGet("/courses", ListAsync).WithName("listCatalogCoursesInternal").WithTags("Catalog");
    }

    private static async Task<IResult> ListAsync(HttpContext context, ITenantContext tenantContext,
        IListCatalogCourses useCase, CancellationToken cancellationToken, int _page = 1, int _size = 10)
    {
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty)
            return Results.Problem(statusCode: 401, title: "Access token tenant is invalid.",
                extensions: new Dictionary<string, object?> { ["code"] = "TOKEN_INVALID" });
        tenantContext.Set(tenantId);
        return Results.Ok(await useCase.ExecuteAsync(new ListCatalogCoursesInput(_page, _size), cancellationToken));
    }
}
