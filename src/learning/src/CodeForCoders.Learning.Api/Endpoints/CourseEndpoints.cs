using CodeForCoders.Learning.Application.UseCases.Courses.ResolveCourseReferences;
using CodeForCoders.Learning.Application.UseCases.Courses.PublishCourse;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Application.UseCases.Courses.CreateCourse;
using CodeForCoders.Learning.Application.UseCases.Courses.GetCourse;
using CodeForCoders.Learning.Application.UseCases.Courses.ListCourses;
using CodeForCoders.Learning.Application.UseCases.Courses.DiscardCourseDraft;
using CodeForCoders.Learning.Application.UseCases.Courses.GetCourseVersion;
using CodeForCoders.Learning.Application.UseCases.Courses.ListCourseVersions;
using CodeForCoders.Learning.Application.UseCases.Courses.DeleteCourse;

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
        group.MapDelete("/{courseId:guid}", DeleteAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapCourseStructureEndpoints();
        group.MapPost("/{courseId:guid}/versions", PublishAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapGet("/{courseId:guid}/versions", ListVersionsAsync).RequireAuthorization(LearningAuthorization.Read);
        group.MapGet("/{courseId:guid}/versions/{versionNumber:int}", GetVersionAsync).RequireAuthorization(LearningAuthorization.Read);
        group.MapPost("/{courseId:guid}/discard-draft", DiscardAsync).RequireAuthorization(LearningAuthorization.Edit);
    }

    private static async Task<IResult> DeleteAsync(Guid courseId, HttpContext context, IDeleteCourse useCase, CancellationToken cancellationToken)
    {
        var write = new CourseWriteContext(courseId, Guid.Parse(context.User.FindFirst("tenantId")!.Value),
            Guid.Parse(context.User.FindFirst("sub")!.Value), context.Request.Headers["X-Actor-Name"].ToString(),
            context.Request.Headers["Idempotency-Key"].ToString(), string.Empty);
        await useCase.ExecuteAsync(new(write), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListVersionsAsync(Guid courseId, IListCourseVersions useCase, CancellationToken cancellationToken, int _page = 1, int _size = 20)
        => Results.Ok(await useCase.ExecuteAsync(new(courseId, _page, _size), cancellationToken));

    private static async Task<IResult> GetVersionAsync(Guid courseId, int versionNumber, IGetCourseVersion useCase, CancellationToken cancellationToken)
        => Results.Ok(await useCase.ExecuteAsync(new(courseId, versionNumber), cancellationToken));

    private static async Task<IResult> DiscardAsync(Guid courseId, DiscardCourseDraftRequest request, HttpContext context, IDiscardCourseDraft useCase, CancellationToken cancellationToken)
    {
        var write = new CourseWriteContext(courseId, Guid.Parse(context.User.FindFirst("tenantId")!.Value),
            Guid.Parse(context.User.FindFirst("sub")!.Value), context.Request.Headers["X-Actor-Name"].ToString(),
            context.Request.Headers["Idempotency-Key"].ToString(), System.Text.Json.JsonSerializer.Serialize(request));
        return Results.Ok(await useCase.ExecuteAsync(new(write, request.DraftRevision), cancellationToken));
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
