using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class MyCoursesEndpoints
{
    public static void MapMyCoursesEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/api/v1/my-courses", ListAsync).WithName("listMyCourses");

    private static async Task<IResult> ListAsync(HttpContext context, IMyCoursesLearningClient client, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        var result = await client.ListAsync(BffSessionContext.GetAccessToken(context)!, cancellationToken);
        return result.StatusCode == 200 ? Results.Json(result.Body)
            : Results.Problem(statusCode: result.StatusCode, title: "Não foi possível carregar seus cursos agora.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = result.Code,
                    ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
                });
    }
}
