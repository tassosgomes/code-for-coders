using System.Net;
using CodeForCoders.BffAdmin.Api.ApiModels;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IVideoUploadClient
{
    Task<VideoUploadApiResult<VideoUploadResponse>> CreateAsync(
        CreateVideoUploadInternalRequest request,
        string accessToken,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<VideoUploadApiResult<VideoUploadPageResponse>> ListPendingAsync(
        int page,
        int size,
        string accessToken,
        CancellationToken cancellationToken);

    Task<VideoUploadApiResult<VideoUploadResponse>> GetAsync(
        Guid uploadId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<VideoUploadApiResult<VideoPartUrlsResponse>> CreatePartUrlsAsync(
        Guid uploadId,
        CreateVideoUploadPartUrlsRequest request,
        string accessToken,
        CancellationToken cancellationToken);

    Task<VideoUploadApiResult<VideoResponse>> CompleteAsync(
        Guid uploadId,
        string accessToken,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public sealed record VideoUploadApiResult<TResponse>(
    HttpStatusCode StatusCode,
    string? Code,
    TResponse? Response);
