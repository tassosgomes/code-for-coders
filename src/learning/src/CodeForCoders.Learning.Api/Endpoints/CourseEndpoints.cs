using CodeForCoders.Learning.Application.UseCases.Courses.ResolveCourseReferences;
using CodeForCoders.Learning.Application.UseCases.Courses.PublishCourse;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
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
        endpoints.MapPost("/internal/v1/course-references/resolve", ResolveAsync).RequireAuthorization(LearningAuthorization.Administrator);
        var group = endpoints.MapGroup("/internal/v1/courses").WithTags("Courses");
        group.MapGet("", ListAsync).RequireAuthorization(LearningAuthorization.Read);
        group.MapGet("/{courseId:guid}", GetAsync).RequireAuthorization(LearningAuthorization.Read);
        group.MapPost("", CreateAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapCourseStructureEndpoints();
        group.MapPost("/{courseId:guid}/versions", PublishAsync).RequireAuthorization(LearningAuthorization.Edit);
    }

    private static async Task<IResult> ResolveAsync(CourseReferenceRequest request, IResolveCourseReferences useCase, CancellationToken cancellationToken)
        => Results.Ok(await useCase.ExecuteAsync(request.CourseIds, cancellationToken));

    private static async Task<IResult> PublishAsync(Guid courseId, PublishCourseRequest request, HttpContext context,
        IPublishCourse useCase, CancellationToken cancellationToken)
    {
        var write = new CourseWriteContext(courseId, Guid.Parse(context.User.FindFirst("tenantId")!.Value),
            Guid.Parse(context.User.FindFirst("sub")!.Value), context.Request.Headers["X-Actor-Name"].ToString(),
            context.Request.Headers["Idempotency-Key"].ToString(), string.Empty);
        var output = await useCase.ExecuteAsync(new(write, request.DraftRevision, request.VersionNote), cancellationToken);
        return Results.Created($"/internal/v1/courses/{courseId:D}/versions/{output.VersionNumber}", output);
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
