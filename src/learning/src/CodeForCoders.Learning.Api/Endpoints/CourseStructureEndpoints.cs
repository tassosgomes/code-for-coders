using System.Text.Json;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Application.UseCases.Courses.UpdateCourse;
using CodeForCoders.Learning.Application.UseCases.Courses.CreateModule;
using CodeForCoders.Learning.Application.UseCases.Courses.UpdateModule;
using CodeForCoders.Learning.Application.UseCases.Courses.DeleteModule;
using CodeForCoders.Learning.Application.UseCases.Courses.CreateLesson;
using CodeForCoders.Learning.Application.UseCases.Courses.UpdateLesson;
using CodeForCoders.Learning.Application.UseCases.Courses.DeleteLesson;

namespace CodeForCoders.Learning.Api.Endpoints;

public static class CourseStructureEndpoints
{
    public static void MapCourseStructureEndpoints(this RouteGroupBuilder group)
    {
        group.MapPatch("/{courseId:guid}", UpdateCourseAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapPost("/{courseId:guid}/modules", CreateModuleAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapPatch("/{courseId:guid}/modules/{moduleId:guid}", UpdateModuleAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapDelete("/{courseId:guid}/modules/{moduleId:guid}", DeleteModuleAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapPost("/{courseId:guid}/modules/{moduleId:guid}/lessons", CreateLessonAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapPatch("/{courseId:guid}/lessons/{lessonId:guid}", UpdateLessonAsync).RequireAuthorization(LearningAuthorization.Edit);
        group.MapDelete("/{courseId:guid}/lessons/{lessonId:guid}", DeleteLessonAsync).RequireAuthorization(LearningAuthorization.Edit);
    }

    private static async Task<IResult> UpdateCourseAsync(Guid courseId, JsonElement body, HttpContext context, IUpdateCourse useCase, CancellationToken cancellationToken)
    {
        var output = await useCase.ExecuteAsync(new UpdateCourseInput(WriteContext(courseId, context, body.GetRawText()), CourseChangesRequest.Parse(body, ["title", "description", "level", "prerequisiteText", "recommendedCourseIds"], false)), cancellationToken);
        return Results.Ok(output.Course);
    }

    private static async Task<IResult> CreateModuleAsync(Guid courseId, JsonElement body, HttpContext context, ICreateModule useCase, CancellationToken cancellationToken)
    {
        var output = await useCase.ExecuteAsync(new CreateModuleInput(WriteContext(courseId, context, body.GetRawText()), CourseChangesRequest.Parse(body, ["title", "position"], true)), cancellationToken);
        return Results.Created($"/internal/v1/courses/{courseId:D}/modules/{output.CreatedId:D}", output.Course);
    }

    private static async Task<IResult> UpdateModuleAsync(Guid courseId, Guid moduleId, JsonElement body, HttpContext context, IUpdateModule useCase, CancellationToken cancellationToken)
    {
        var output = await useCase.ExecuteAsync(new UpdateModuleInput(WriteContext(courseId, context, body.GetRawText()), moduleId, CourseChangesRequest.Parse(body, ["title", "position"], false)), cancellationToken);
        return Results.Ok(output.Course);
    }

    private static async Task<IResult> DeleteModuleAsync(Guid courseId, Guid moduleId, HttpContext context, IDeleteModule useCase, CancellationToken cancellationToken)
    {
        var output = await useCase.ExecuteAsync(new DeleteModuleInput(WriteContext(courseId, context, ""), moduleId), cancellationToken);
        return Results.Ok(output.Course);
    }

    private static async Task<IResult> CreateLessonAsync(Guid courseId, Guid moduleId, JsonElement body, HttpContext context, ICreateLesson useCase, CancellationToken cancellationToken)
    {
        var output = await useCase.ExecuteAsync(new CreateLessonInput(WriteContext(courseId, context, body.GetRawText()), moduleId, CourseChangesRequest.Parse(body, ["title", "description", "position", "videoId"], true)), cancellationToken);
        return Results.Created($"/internal/v1/courses/{courseId:D}/modules/{moduleId:D}/lessons/{output.CreatedId:D}", output.Course);
    }

    private static async Task<IResult> UpdateLessonAsync(Guid courseId, Guid lessonId, JsonElement body, HttpContext context, IUpdateLesson useCase, CancellationToken cancellationToken)
    {
        var output = await useCase.ExecuteAsync(new UpdateLessonInput(WriteContext(courseId, context, body.GetRawText()), lessonId, CourseChangesRequest.Parse(body, ["title", "description", "position", "moduleId", "videoId"], false)), cancellationToken);
        return Results.Ok(output.Course);
    }

    private static async Task<IResult> DeleteLessonAsync(Guid courseId, Guid lessonId, HttpContext context, IDeleteLesson useCase, CancellationToken cancellationToken)
    {
        var output = await useCase.ExecuteAsync(new DeleteLessonInput(WriteContext(courseId, context, ""), lessonId), cancellationToken);
        return Results.Ok(output.Course);
    }

    private static CourseWriteContext WriteContext(Guid courseId, HttpContext context, string requestJson)
        => new(courseId, Guid.Parse(context.User.FindFirst("tenantId")!.Value), Guid.Parse(context.User.FindFirst("sub")!.Value),
            context.Request.Headers["X-Actor-Name"].ToString(), context.Request.Headers["Idempotency-Key"].ToString(), requestJson);
}
