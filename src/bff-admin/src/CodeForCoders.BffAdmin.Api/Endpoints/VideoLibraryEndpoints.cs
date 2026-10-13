using System.Net;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class VideoLibraryEndpoints
{
    private const string VideoPermission = "midia.enviar";

    public static void MapVideoLibraryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/videos/{videoId:guid}/transcription", GetVideoTranscriptionAsync)
            .WithName("GetVideoTranscription")
            .WithTags("Videos")
            .Produces<VideoTranscriptionContentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
        endpoints.MapPost("/api/v1/videos/{videoId:guid}/transcription-requests", RequestVideoTranscriptionAsync)
            .WithName("RequestVideoTranscription")
            .WithTags("Videos")
            .Produces<VideoResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapGet("/api/v1/video-tags", ListVideoTagsAsync)
            .WithName("ListVideoTags")
            .WithTags("Videos")
            .Produces<VideoTagListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapGet("/api/v1/videos", ListVideosAsync)
            .WithName("ListVideos")
            .WithTags("Videos")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces<VideoPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapGet("/api/v1/videos/{videoId:guid}", GetVideoAsync)
            .WithName("GetVideo")
            .WithTags("Videos")
            .Produces<VideoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapPut("/api/v1/videos/{videoId:guid}/tags", ReplaceVideoTagsAsync)
            .WithName("ReplaceVideoTags")
            .WithTags("Videos")
            .Produces<VideoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        endpoints.MapPatch("/api/v1/videos/{videoId:guid}", UpdateVideoTitleAsync)
            .WithName("UpdateVideoTitle")
            .WithTags("Videos")
            .Produces<VideoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static async Task<IResult> GetVideoTranscriptionAsync(
        Guid videoId, HttpContext httpContext, IStaffSessionIdentityClient identityClient,
        IVideoLibraryClient mediaClient, CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "private, no-store";
        var access = await GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null) return access.Problem;
        var result = await mediaClient.GetVideoTranscriptionAsync(videoId, access.AccessToken!, cancellationToken);
        if (result.StatusCode != HttpStatusCode.OK || result.Body is null)
            return MediaProblem(httpContext, new(result.StatusCode, result.Code, null, null));
        httpContext.Response.Headers.CacheControl = result.CacheControl;
        return Results.Bytes(result.Body, result.ContentType ?? "application/json");
    }

    private static async Task<IResult> RequestVideoTranscriptionAsync(
        Guid videoId, HttpContext httpContext, IStaffSessionIdentityClient identityClient,
        IVideoLibraryClient mediaClient, CancellationToken cancellationToken)
    {
        var access = await GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null) return access.Problem;
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid idempotency key is required.");
        var result = await mediaClient.RequestVideoTranscriptionAsync(videoId, key, access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.Accepted && result.Video is not null
            ? Results.Accepted($"/api/v1/videos/{videoId:D}", result.Video) : MediaProblem(httpContext, result);
    }

    private static async Task<IResult> ListVideoTagsAsync(
        HttpContext httpContext, IStaffSessionIdentityClient identityClient,
        IVideoLibraryClient mediaClient, CancellationToken cancellationToken, string? q = null)
    {
        var access = await GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null) return access.Problem;
        if (q is not null && (string.IsNullOrWhiteSpace(q) || q.Length > 100))
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The tag query is invalid.");
        var result = await mediaClient.ListVideoTagsAsync(q, access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.OK && result.Tags is not null
            ? Results.Ok(result.Tags) : MediaProblem(httpContext, result);
    }

    private static async Task<IResult> ListVideosAsync(
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IVideoLibraryClient mediaClient,
        CancellationToken cancellationToken,
        int _page = 1,
        int _size = 10,
        string[]? status = null,
        string? q = null,
        string[]? tag = null,
        string tagMatch = "all")
    {
        if (_page < 1 || _size is < 1 or > 50 || (long)(_page - 1) * _size > int.MaxValue
            || status?.Any(value => value is not ("received" or "preparing" or "ready" or "failed")) == true
            || (q is not null && (string.IsNullOrWhiteSpace(q) || q.Length > 120))
            || tag?.Length > 10
            || tag?.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 100) == true
            || tagMatch is not ("all" or "any"))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The requested page is invalid.");
        }

        var access = await GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null)
        {
            return access.Problem;
        }

        var result = await mediaClient.ListVideosAsync(new(_page, _size, status ?? [], q?.Trim(), tag ?? [], tagMatch), access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.OK && result.Page is not null
            ? Results.Ok(result.Page)
            : MediaProblem(httpContext, result);
    }

    private static async Task<IResult> UpdateVideoTitleAsync(
        Guid videoId,
        UpdateVideoTitleRequest request,
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IVideoLibraryClient mediaClient,
        CancellationToken cancellationToken)
    {
        var access = await GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null) return access.Problem;
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid idempotency key is required.");
        }

        var result = await mediaClient.UpdateVideoTitleAsync(videoId, request.Title, key, access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.OK && result.Video is not null
            ? Results.Ok(result.Video)
            : MediaProblem(httpContext, result);
    }

    private static async Task<IResult> ReplaceVideoTagsAsync(
        Guid videoId, ReplaceVideoTagsRequest request, HttpContext httpContext,
        IStaffSessionIdentityClient identityClient, IVideoLibraryClient mediaClient, CancellationToken cancellationToken)
    {
        var access = await GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null) return access.Problem;
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || request.Tags is null
            || request.Tags.Count > 50 || request.Tags.Any(tag => tag is null || tag.Length > 100))
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The video tags request is invalid.");
        var result = await mediaClient.ReplaceVideoTagsAsync(videoId, request.Tags, key, access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.OK && result.Video is not null
            ? Results.Ok(result.Video) : MediaProblem(httpContext, result);
    }

    private static async Task<IResult> GetVideoAsync(
        Guid videoId,
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IVideoLibraryClient mediaClient,
        CancellationToken cancellationToken)
    {
        var access = await GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null)
        {
            return access.Problem;
        }

        var result = await mediaClient.GetVideoAsync(videoId, access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.OK && result.Video is not null
            ? Results.Ok(result.Video)
            : MediaProblem(httpContext, result);
    }

    internal static async Task<MediaAccess> GetMediaAccessAsync(
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        var session = BffSessionContext.Get(httpContext);
        var currentValidation = BffSessionContext.GetValidatedSession(httpContext);
        if (session is null || currentValidation is null)
        {
            return new MediaAccess(null, null, Problem(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "SESSION_REQUIRED",
                "A current staff session is required."));
        }

        if (!currentValidation.Permissions.Contains(VideoPermission, StringComparer.Ordinal))
        {
            return new MediaAccess(null, null, Problem(
                httpContext,
                StatusCodes.Status403Forbidden,
                "PERMISSION_DENIED",
                "Você não tem permissão para esta área."));
        }

        var validation = await identityClient.ValidateSessionAsync(
            session.IdentitySessionId,
            "media",
            cancellationToken);
        if (validation.StatusCode == StatusCodes.Status401Unauthorized && validation.Code == "SESSION_REQUIRED")
        {
            return new MediaAccess(null, null, Problem(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "SESSION_REQUIRED",
                "A current staff session is required."));
        }

        if (validation.StatusCode != StatusCodes.Status200OK
            || validation.Session is null
            || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
        {
            var statusCode = validation.StatusCode == StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;
            return new MediaAccess(null, null, Problem(
                httpContext,
                statusCode,
                "IDENTITY_UNAVAILABLE",
                "The staff identity service is temporarily unavailable."));
        }

        return new MediaAccess(validation.Session.AccessToken, validation.Session.Name, null);
    }

    private static IResult MediaProblem(HttpContext httpContext, VideoLibraryResult result)
    {
        if (result.StatusCode == HttpStatusCode.Unauthorized && result.Code == "TOKEN_INVALID")
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "TOKEN_INVALID", "Token de acesso inválido.");
        }

        if (result.StatusCode == HttpStatusCode.Forbidden && result.Code == "PERMISSION_DENIED")
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "Você não tem permissão para esta área.");
        }

        if (result.StatusCode == HttpStatusCode.NotFound && result.Code == "VIDEO_NOT_FOUND")
        {
            return Problem(httpContext, StatusCodes.Status404NotFound, "VIDEO_NOT_FOUND", "The requested video was not found.");
        }

        if (result.StatusCode == HttpStatusCode.UnprocessableEntity && result.Code is "TITLE_REQUIRED" or "IDEMPOTENCY_KEY_REUSED" or "TAG_INVALID" or "TAGS_LIMIT_EXCEEDED")
        {
            return Problem(httpContext, StatusCodes.Status422UnprocessableEntity, result.Code, "The video could not be updated.");
        }

        if (result.StatusCode == HttpStatusCode.Conflict && result.Code is "VIDEO_NOT_READY" or "TRANSCRIPTION_NOT_RETRYABLE" or "TRANSCRIPTION_NOT_READY")
            return Problem(httpContext, StatusCodes.Status409Conflict, result.Code, "The video cannot be transcribed.");

        if (result.StatusCode == HttpStatusCode.BadRequest)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The video request is invalid.");
        }

        var gatewayStatus = result.StatusCode == HttpStatusCode.GatewayTimeout
            ? StatusCodes.Status504GatewayTimeout
            : StatusCodes.Status502BadGateway;
        return Problem(
            httpContext,
            gatewayStatus,
            "MEDIA_UNAVAILABLE",
            "The video service is temporarily unavailable.");
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

    internal sealed record MediaAccess(string? AccessToken, string? UploaderName, IResult? Problem);
}
