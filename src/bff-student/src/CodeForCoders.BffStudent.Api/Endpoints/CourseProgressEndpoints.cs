using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class CourseProgressEndpoints
{
    public static void MapCourseProgressEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/api/v1/courses/{courseId:guid}/progress", GetAsync);

    private static async Task<IResult> GetAsync(Guid courseId, HttpContext context, ICourseProgressLearningClient client, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        var result = await client.GetAsync(courseId, BffSessionContext.GetAccessToken(context)!, cancellationToken);
        if (result.StatusCode == 200) return Results.Json(result.Body);
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = result.Code,
            ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
        };
        if (result.Reason is not null) extensions["reason"] = result.Reason;
        if (result.AccessEndedAt.HasValue) extensions["accessEndedAt"] = result.AccessEndedAt.Value;
        return Results.Problem(statusCode: result.StatusCode, title: "Não foi possível carregar o progresso.", extensions: extensions);
    }
}
