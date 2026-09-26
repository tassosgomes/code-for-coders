using System.Net;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Api.Clients;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class VideoUploadEndpoints
{
    public static void MapVideoUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/video-uploads", CreateAsync)
            .WithName("CreateVideoUpload")
            .WithTags("Video uploads")
            .Produces<VideoUploadResponse>(StatusCodes.Status200OK)
            .Produces<VideoUploadResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapGet("/api/v1/video-uploads", ListPendingAsync)
            .WithName("ListPendingVideoUploads")
            .WithTags("Video uploads")
            .Produces<VideoUploadPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapGet("/api/v1/video-uploads/{uploadId:guid}", GetAsync)
            .WithName("GetVideoUpload")
            .WithTags("Video uploads")
            .Produces<VideoUploadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapPost("/api/v1/video-uploads/{uploadId:guid}/part-urls", CreatePartUrlsAsync)
            .WithName("CreateVideoUploadPartUrls")
            .WithTags("Video uploads")
            .Produces<VideoPartUrlsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapPost("/api/v1/video-uploads/{uploadId:guid}/complete", CompleteAsync)
            .WithName("CompleteVideoUpload")
            .WithTags("Video uploads")
            .Produces<VideoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> CreateAsync(
        HttpContext httpContext,
        CreateVideoUploadRequest request,
        IStaffSessionIdentityClient identityClient,
        IVideoUploadClient mediaClient,
        CancellationToken cancellationToken)
    {
        var access = await VideoLibraryEndpoints.GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null)
        {
            return access.Problem;
        }

        var idempotencyKey = ReadIdempotencyKey(httpContext);
        if (idempotencyKey is null)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid idempotency key is required.");
        }

        var result = await mediaClient.CreateAsync(
            new CreateVideoUploadInternalRequest(
                request.Title,
                request.FileName,
                request.FileSize,
                request.ContentType,
                request.Fingerprint,
                access.UploaderName!),
            access.AccessToken!,
            idempotencyKey,
            cancellationToken);
        if (result.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK && result.Response is not null)
        {
            var location = $"/api/v1/video-uploads/{result.Response.UploadId:D}";
            httpContext.Response.Headers.Location = location;
            return result.StatusCode == HttpStatusCode.Created
                ? Results.Created(location, result.Response)
                : Results.Ok(result.Response);
        }

        return MediaProblem(httpContext, result.StatusCode, result.Code);
    }

    private static async Task<IResult> ListPendingAsync(
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IVideoUploadClient mediaClient,
        CancellationToken cancellationToken,
        int _page = 1,
        int _size = 10)
    {
        if (_page < 1 || _size is < 1 or > 50 || (long)(_page - 1) * _size > int.MaxValue)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The requested page is invalid.");
        }

        var access = await VideoLibraryEndpoints.GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null)
        {
            return access.Problem;
        }

        var result = await mediaClient.ListPendingAsync(_page, _size, access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.OK && result.Response is not null
            ? Results.Ok(result.Response)
            : MediaProblem(httpContext, result.StatusCode, result.Code);
    }

    private static async Task<IResult> GetAsync(
        Guid uploadId,
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IVideoUploadClient mediaClient,
        CancellationToken cancellationToken)
    {
        var access = await VideoLibraryEndpoints.GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null)
        {
            return access.Problem;
        }

        var result = await mediaClient.GetAsync(uploadId, access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.OK && result.Response is not null
            ? Results.Ok(result.Response)
            : MediaProblem(httpContext, result.StatusCode, result.Code);
    }

    private static async Task<IResult> CreatePartUrlsAsync(
        Guid uploadId,
        HttpContext httpContext,
        CreateVideoUploadPartUrlsRequest request,
        IStaffSessionIdentityClient identityClient,
        IVideoUploadClient mediaClient,
        CancellationToken cancellationToken)
    {
        var access = await VideoLibraryEndpoints.GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null)
        {
            return access.Problem;
        }

        var result = await mediaClient.CreatePartUrlsAsync(uploadId, request, access.AccessToken!, cancellationToken);
        return result.StatusCode == HttpStatusCode.OK && result.Response is not null
            ? Results.Ok(result.Response)
            : MediaProblem(httpContext, result.StatusCode, result.Code);
    }

    private static async Task<IResult> CompleteAsync(
        Guid uploadId,
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IVideoUploadClient mediaClient,
        CancellationToken cancellationToken)
    {
        var access = await VideoLibraryEndpoints.GetMediaAccessAsync(httpContext, identityClient, cancellationToken);
        if (access.Problem is not null)
        {
            return access.Problem;
        }

        var idempotencyKey = ReadIdempotencyKey(httpContext);
        if (idempotencyKey is null)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid idempotency key is required.");
        }

        var result = await mediaClient.CompleteAsync(uploadId, access.AccessToken!, idempotencyKey, cancellationToken);
        if (result.StatusCode == HttpStatusCode.Created && result.Response is not null)
        {
            var location = $"/api/v1/videos/{result.Response.VideoId:D}";
            httpContext.Response.Headers.Location = location;
            return Results.Created(location, result.Response);
        }

        return MediaProblem(httpContext, result.StatusCode, result.Code);
    }

    private static string? ReadIdempotencyKey(HttpContext httpContext)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(key) && key.Length <= 128 ? key : null;
    }

    private static IResult MediaProblem(HttpContext httpContext, HttpStatusCode statusCode, string? code)
    {
        if (statusCode == HttpStatusCode.Unauthorized && code == "TOKEN_INVALID")
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, code, "Token de acesso inválido.");
        }

        if (statusCode == HttpStatusCode.Forbidden && code == "PERMISSION_DENIED")
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, code, "Você não tem permissão para esta área.");
        }

        if (statusCode == HttpStatusCode.NotFound && code == "UPLOAD_NOT_FOUND")
        {
            return Problem(httpContext, StatusCodes.Status404NotFound, code, "The video upload was not found.");
        }

        if (statusCode == HttpStatusCode.UnprocessableEntity && code is not null)
        {
            return Problem(httpContext, StatusCodes.Status422UnprocessableEntity, code, "The video upload request could not be completed.");
        }

        var gatewayStatus = statusCode == HttpStatusCode.GatewayTimeout
            ? StatusCodes.Status504GatewayTimeout
            : StatusCodes.Status502BadGateway;
        return Problem(httpContext, gatewayStatus, "MEDIA_UNAVAILABLE", "The video service is temporarily unavailable.");
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
