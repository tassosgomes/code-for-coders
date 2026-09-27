using CodeForCoders.Media.Api.Security;
using CodeForCoders.Media.Application.UseCases.Videos.GetVideo;
using CodeForCoders.Media.Application.UseCases.Videos.ListVideos;
using CodeForCoders.Media.Application.UseCases.Videos;
using CodeForCoders.Media.Application.UseCases.Videos.UpdateVideoTitle;
using CodeForCoders.Media.Api.ApiModels;

namespace CodeForCoders.Media.Api.Endpoints;

public static class VideoEndpoints
{
    public static void MapVideoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1")
            .RequireAuthorization(MediaAuthorization.PolicyName)
            .WithTags("Videos");

        group.MapGet("/videos", ListVideosAsync)
            .WithName("ListVideosInternal")
            .Produces<VideoPageOutput>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/videos/{videoId:guid}", GetVideoAsync)
            .WithName("GetVideoInternal")
            .Produces<VideoOutput>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/videos/{videoId:guid}", UpdateVideoTitleAsync)
            .WithName("UpdateVideoTitleInternal")
            .Produces<VideoOutput>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> ListVideosAsync(
        HttpContext httpContext,
        IListVideos listVideos,
        CancellationToken cancellationToken,
        int _page = 1,
        int _size = 10,
        string[]? status = null,
        string? q = null)
    {
        if (_page < 1 || _size is < 1 or > 50 || (long)(_page - 1) * _size > int.MaxValue
            || status?.Any(value => value is not ("received" or "preparing" or "ready" or "failed")) == true
            || (q is not null && (string.IsNullOrWhiteSpace(q) || q.Length > 120)))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The requested page is invalid.");
        }

        var page = await listVideos.ExecuteAsync(new ListVideosInput(_page, _size, status ?? [], q?.Trim()), cancellationToken);
        return Results.Ok(page);
    }

    private static async Task<IResult> UpdateVideoTitleAsync(
        Guid videoId,
        UpdateVideoTitleRequest request,
        HttpContext httpContext,
        IUpdateVideoTitle updateVideoTitle,
        CancellationToken cancellationToken)
        => Results.Ok(await updateVideoTitle.ExecuteAsync(
            new UpdateVideoTitleInput(videoId, request.Title, httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty),
            cancellationToken));

    private static async Task<IResult> GetVideoAsync(
        Guid videoId,
        IGetVideo getVideo,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var video = await getVideo.ExecuteAsync(new GetVideoInput(videoId), cancellationToken);
        return video is null
            ? Problem(httpContext, StatusCodes.Status404NotFound, "VIDEO_NOT_FOUND", "The requested video was not found.")
            : Results.Ok(video);
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
