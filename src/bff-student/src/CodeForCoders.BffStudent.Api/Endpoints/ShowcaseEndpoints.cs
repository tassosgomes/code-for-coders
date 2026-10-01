using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class ShowcaseEndpoints
{
    public const string Prefix = "/api/v1/showcase";

    private const int DefaultSize = 12;
    private const int MaxSize = 48;

    private static readonly string[] Levels = ["beginner", "intermediate", "advanced"];

    public static void MapShowcaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"{Prefix}/courses", ListCoursesAsync)
            .WithName("listShowcaseCourses")
            .WithTags("Showcase")
            .Produces<ShowcaseCoursePageV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> ListCoursesAsync(
        HttpContext httpContext,
        IShowcaseCommerceClient client,
        CancellationToken cancellationToken,
        string? level = null,
        int _page = 1,
        int _size = DefaultSize)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        if ((level is not null && !Levels.Contains(level, StringComparer.Ordinal)) || _page < 1 || _size is < 1 or > MaxSize)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The showcase request is invalid.");
        }

        var result = await client.ListCoursesAsync(level, _page, _size, cancellationToken);
        if (result.StatusCode == StatusCodes.Status200OK && result.Page is not null)
        {
            return Results.Ok(result.Page);
        }

        return result.StatusCode switch
        {
            StatusCodes.Status400BadRequest => Problem(httpContext, result.StatusCode, "INVALID_REQUEST", "The showcase request is invalid."),
            StatusCodes.Status504GatewayTimeout => Problem(httpContext, result.StatusCode, "SHOWCASE_TIMEOUT", "The showcase took too long to respond."),
            _ => Problem(httpContext, StatusCodes.Status502BadGateway, "SHOWCASE_UNAVAILABLE", "The showcase is temporarily unavailable."),
        };
    }

    private static IResult Problem(HttpContext httpContext, int statusCode, string code, string title)
        => Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            });
}
