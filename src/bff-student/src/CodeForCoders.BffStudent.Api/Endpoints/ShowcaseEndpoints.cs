using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using CodeForCoders.BffStudent.Api.Extensions;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class ShowcaseEndpoints
{
    public const string Prefix = "/api/v1/showcase";

    private const int DefaultSize = 12;
    private const int MaxSize = 48;

    private static readonly string[] Levels = ["beginner", "intermediate", "advanced"];

    public static void MapShowcaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost($"{Prefix}/offers/{{offerId}}/purchase-intents", RegisterPurchaseIntentAsync)
            .RequireRateLimiting(PurchaseIntentRateLimitExtensions.Policy)
            .WithName("registerPurchaseIntent").WithTags("Showcase");
        endpoints.MapGet($"{Prefix}/courses", ListCoursesAsync)
            .WithName("listShowcaseCourses")
            .WithTags("Showcase")
            .Produces<ShowcaseCoursePageV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
        endpoints.MapGet($"{Prefix}/courses/{{courseId}}", GetCourseAsync)
            .WithName("getShowcaseCourse")
            .WithTags("Showcase")
            .Produces<ShowcaseCourseDetailV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> RegisterPurchaseIntentAsync(HttpContext httpContext, IShowcaseCommerceClient client,
        CancellationToken cancellationToken, string offerId)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParseExact(offerId, "D", out var id))
            return Problem(httpContext, StatusCodes.Status404NotFound, "OFFER_NOT_AVAILABLE", "Oferta não disponível.");
        var keys = httpContext.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || string.IsNullOrWhiteSpace(keys[0]) || keys[0]!.Length > 128)
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A purchase intent key is required.");
        var result = await client.RegisterPurchaseIntentAsync(id, keys[0]!, cancellationToken);
        if (result.StatusCode == StatusCodes.Status202Accepted && result.Body is not null)
            return Results.Json(result.Body, statusCode: StatusCodes.Status202Accepted);
        return result.StatusCode switch
        {
            StatusCodes.Status404NotFound => Problem(httpContext, result.StatusCode, "OFFER_NOT_AVAILABLE", "Oferta não disponível."),
            StatusCodes.Status504GatewayTimeout => Problem(httpContext, result.StatusCode, "SHOWCASE_TIMEOUT", "The showcase took too long to respond."),
            _ => Problem(httpContext, StatusCodes.Status502BadGateway, "SHOWCASE_UNAVAILABLE", "The showcase is temporarily unavailable."),
        };
    }

    private static async Task<IResult> GetCourseAsync(
        HttpContext httpContext,
        IShowcaseCommerceClient client,
        CancellationToken cancellationToken,
        string courseId)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        // An address that is not even an identifier gets the same answer as a course that is not on sale (RN-O02).
        if (!Guid.TryParseExact(courseId, "D", out var id))
        {
            return CourseNotFound(httpContext);
        }

        var result = await client.GetCourseAsync(id, cancellationToken);
        if (result.StatusCode == StatusCodes.Status200OK && result.Body is not null)
        {
            return Results.Ok(result.Body);
        }

        return result.StatusCode switch
        {
            StatusCodes.Status404NotFound => CourseNotFound(httpContext),
            StatusCodes.Status504GatewayTimeout => Problem(httpContext, result.StatusCode, "SHOWCASE_TIMEOUT", "The showcase took too long to respond."),
            _ => Problem(httpContext, StatusCodes.Status502BadGateway, "SHOWCASE_UNAVAILABLE", "The showcase is temporarily unavailable."),
        };
    }

    private static IResult CourseNotFound(HttpContext httpContext)
        => Problem(httpContext, StatusCodes.Status404NotFound, ShowcaseCommerceClient.CourseNotFoundCode, "Curso não disponível.");

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
        if (result.StatusCode == StatusCodes.Status200OK && result.Body is not null)
        {
            return Results.Ok(result.Body);
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
