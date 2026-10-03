using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.UseCases.StudentLessons.GetStudentLesson;

namespace CodeForCoders.Learning.Api.Endpoints;

public static class StudentLessonEndpoints
{
    public static void MapStudentLessonEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/internal/v1/lessons/{lessonId:guid}", GetAsync).RequireAuthorization(LearningAuthorization.Student);

    private static async Task<IResult> GetAsync(Guid lessonId, HttpContext context, IGetStudentLesson useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        var result = await useCase.ExecuteAsync(new(lessonId, Guid.Parse(context.User.FindFirst("sub")!.Value)), cancellationToken);
        return Results.Ok(result);
    }
}
