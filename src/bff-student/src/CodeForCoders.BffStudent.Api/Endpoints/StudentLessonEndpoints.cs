using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class StudentLessonEndpoints
{
    public static void MapStudentLessonEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/api/v1/lessons/{lessonId:guid}", GetAsync);

    private static async Task<IResult> GetAsync(Guid lessonId, HttpContext context, IStudentLessonLearningClient client, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        var result = await client.GetAsync(lessonId, BffSessionContext.GetAccessToken(context)!, cancellationToken);
        if (result.StatusCode == 200) return Results.Json(result.Body);
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = result.Code,
            ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
        };
        if (result.Reason is not null) extensions["reason"] = result.Reason;
        if (result.AccessEndedAt.HasValue) extensions["accessEndedAt"] = result.AccessEndedAt.Value;
        return Results.Problem(statusCode: result.StatusCode, title: "Não foi possível abrir a aula.", extensions: extensions);
    }
}
