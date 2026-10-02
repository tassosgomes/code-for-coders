using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.StudentAccessGrants.ListStudentAccessGrants;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class StudentAccessGrantEndpoints
{
    public static void MapStudentAccessGrantEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/internal/v1/students/{studentId:guid}/access-grants", ListAsync)
            .RequireAuthorization(CourtesyPolicies.Grant).WithTags("Courtesies").WithName("listStudentAccessGrantsInternal");

    private static async Task<IResult> ListAsync(Guid studentId, HttpContext context, ITenantContext tenant,
        IListStudentAccessGrants useCase, CancellationToken cancellationToken, int _page = 1, int _size = 10)
    {
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty)
            return Results.Problem(statusCode: 401, title: "Access token tenant is invalid.",
                extensions: new Dictionary<string, object?> { ["code"] = "TOKEN_INVALID" });
        tenant.Set(tenantId);
        return Results.Ok(await useCase.ExecuteAsync(new(studentId, _page, _size), cancellationToken));
    }
}
