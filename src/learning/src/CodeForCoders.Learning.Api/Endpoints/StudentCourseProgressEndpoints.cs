using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.UseCases.Progress.GetStudentCourseProgress;

namespace CodeForCoders.Learning.Api.Endpoints;

public static class StudentCourseProgressEndpoints
{
    public static void MapStudentCourseProgressEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/internal/v1/student-courses/{courseId:guid}/progress", GetAsync).RequireAuthorization(LearningAuthorization.Student);

    private static async Task<IResult> GetAsync(Guid courseId, HttpContext context, IGetStudentCourseProgress useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        var result = await useCase.ExecuteAsync(new(courseId, Guid.Parse(context.User.FindFirst("sub")!.Value)), cancellationToken);
        return Results.Ok(result);
    }
}
