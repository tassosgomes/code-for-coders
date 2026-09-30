using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Application.UseCases.Courses.CreateCourse;
using CodeForCoders.Learning.Application.UseCases.Courses.GetCourse;
using CodeForCoders.Learning.Application.UseCases.Courses.ListCourses;

namespace CodeForCoders.Learning.Api.Endpoints;

public static class CourseEndpoints
{
    public static void MapCourseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1/courses").WithTags("Courses");
        group.MapGet("", ListAsync).RequireAuthorization(LearningAuthorization.Read);
        group.MapGet("/{courseId:guid}", GetAsync).RequireAuthorization(LearningAuthorization.Read);
        group.MapPost("", CreateAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapCourseStructureEndpoints();
    }

    private static async Task<IResult> ListAsync(HttpContext context, IListCourses useCase, CancellationToken cancellationToken,
        int _page = 1, int _size = 20, string? status = null)
        => Results.Ok(await useCase.ExecuteAsync(new CourseListQuery(_page, _size, status), cancellationToken));

    private static async Task<IResult> GetAsync(Guid courseId, IGetCourse useCase, CancellationToken cancellationToken)
        => Results.Ok(await useCase.ExecuteAsync(courseId, cancellationToken));

    private static async Task<IResult> CreateAsync(CreateCourseRequest request, HttpContext context, ICreateCourse useCase, CancellationToken cancellationToken)
    {
        var input = new CreateCourseInput(Guid.Parse(context.User.FindFirst("tenantId")!.Value),
            Guid.Parse(context.User.FindFirst("sub")!.Value), context.Request.Headers["X-Actor-Name"].ToString(),
            context.Request.Headers["Idempotency-Key"].ToString(), request.Title, request.Description);
        var output = await useCase.ExecuteAsync(input, cancellationToken);
        return Results.Created($"/internal/v1/courses/{output.CourseId:D}", output);
    }
}
