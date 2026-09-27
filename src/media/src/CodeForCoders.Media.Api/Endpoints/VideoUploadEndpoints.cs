using CodeForCoders.Media.Api.ApiModels;
using CodeForCoders.Media.Api.Security;
using CodeForCoders.Media.Application.UseCases.VideoUploads;
using CodeForCoders.Media.Application.UseCases.VideoUploads.CompleteVideoUpload;
using CodeForCoders.Media.Application.UseCases.VideoUploads.CreateVideoUpload;
using CodeForCoders.Media.Application.UseCases.VideoUploads.CreateVideoUploadPartUrls;
using CodeForCoders.Media.Application.UseCases.VideoUploads.GetVideoUpload;
using CodeForCoders.Media.Application.UseCases.VideoUploads.ListPendingVideoUploads;

namespace CodeForCoders.Media.Api.Endpoints;

public static class VideoUploadEndpoints
{
    public static void MapVideoUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1")
            .RequireAuthorization(MediaAuthorization.PolicyName)
            .WithTags("Video uploads");

        group.MapPost("/video-uploads", CreateVideoUploadAsync)
            .WithName("CreateVideoUploadInternal")
            .Produces<VideoUploadOutput>(StatusCodes.Status200OK)
            .Produces<VideoUploadOutput>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/video-uploads", ListPendingVideoUploadsAsync)
            .WithName("ListPendingVideoUploadsInternal")
            .Produces<VideoUploadPageOutput>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/video-uploads/{uploadId:guid}", GetVideoUploadAsync)
            .WithName("GetVideoUploadInternal")
            .Produces<VideoUploadOutput>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/video-uploads/{uploadId:guid}/part-urls", CreateVideoUploadPartUrlsAsync)
            .WithName("CreateVideoUploadPartUrlsInternal")
            .Produces<VideoPartUrlsOutput>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/video-uploads/{uploadId:guid}/complete", CompleteVideoUploadAsync)
            .WithName("CompleteVideoUploadInternal")
            .Produces<CodeForCoders.Media.Application.UseCases.Videos.VideoOutput>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> CreateVideoUploadAsync(
        HttpContext httpContext,
        CreateVideoUploadRequest request,
        ICreateVideoUpload createVideoUpload,
        CancellationToken cancellationToken)
    {
        var result = await createVideoUpload.ExecuteAsync(
            new CreateVideoUploadInput(
                request.Title,
                request.FileName,
                request.FileSize,
                request.ContentType,
                request.Fingerprint,
                request.UploaderName,
                httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty),
            cancellationToken);
        return result.Resumed
            ? Results.Ok(result.Upload)
            : Results.Created($"/internal/v1/video-uploads/{result.Upload.UploadId:D}", result.Upload);
    }

    private static async Task<IResult> ListPendingVideoUploadsAsync(
        IListPendingVideoUploads listPendingVideoUploads,
        CancellationToken cancellationToken,
        int _page = 1,
        int _size = 10)
        => Results.Ok(await listPendingVideoUploads.ExecuteAsync(
            new ListPendingVideoUploadsInput(_page, _size),
            cancellationToken));

    private static async Task<IResult> GetVideoUploadAsync(
        Guid uploadId,
        IGetVideoUpload getVideoUpload,
        CancellationToken cancellationToken)
        => Results.Ok(await getVideoUpload.ExecuteAsync(new GetVideoUploadInput(uploadId), cancellationToken));

    private static async Task<IResult> CreateVideoUploadPartUrlsAsync(
        Guid uploadId,
        CreateVideoUploadPartUrlsRequest request,
        ICreateVideoUploadPartUrls createPartUrls,
        CancellationToken cancellationToken)
        => Results.Ok(await createPartUrls.ExecuteAsync(
            new CreateVideoUploadPartUrlsInput(uploadId, request.PartNumbers),
            cancellationToken));

    private static async Task<IResult> CompleteVideoUploadAsync(
        Guid uploadId,
        HttpContext httpContext,
        ICompleteVideoUpload completeVideoUpload,
        CancellationToken cancellationToken)
    {
        var result = await completeVideoUpload.ExecuteAsync(
            new CompleteVideoUploadInput(uploadId, httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty),
            cancellationToken);
        return Results.Created($"/internal/v1/videos/{result.Video.VideoId:D}", result.Video);
    }
}
