using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.UseCases.Progress.ListStudentCourses;

namespace CodeForCoders.Learning.Api.Endpoints;

public static class StudentCoursesEndpoints
{
    public static void MapStudentCoursesEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/internal/v1/student-courses", ListAsync)
            .RequireAuthorization(LearningAuthorization.Student).WithName("listStudentCoursesInternal");

    private static async Task<IResult> ListAsync(HttpContext context, IListStudentCourses useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await useCase.ExecuteAsync(new(Guid.Parse(context.User.FindFirst("sub")!.Value)), cancellationToken));
    }
}
