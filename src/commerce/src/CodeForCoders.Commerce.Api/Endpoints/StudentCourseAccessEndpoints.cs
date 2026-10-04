using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.UseCases.Entitlement.ListStudentCourseAccess;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class StudentCourseAccessEndpoints
{
    public static void MapStudentCourseAccessEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGroup("/internal/v1/course-access").RequireAuthorization(StudentCourseAccessPolicies.Read)
            .WithTags("Course access").MapGet("", ListAsync).WithName("listStudentCourseAccessInternal");

    private static async Task<IResult> ListAsync([AsParameters] ListStudentCourseAccessInput input, HttpContext context,
        IListStudentCourseAccess useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await useCase.ExecuteAsync(input, cancellationToken));
    }
}
