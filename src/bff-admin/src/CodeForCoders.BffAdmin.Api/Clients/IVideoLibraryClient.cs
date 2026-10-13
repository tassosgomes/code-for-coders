using System.Net;
using CodeForCoders.BffAdmin.Api.ApiModels;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IVideoLibraryClient
{
    Task<VideoTranscriptionReadResult> GetVideoTranscriptionAsync(Guid videoId, string accessToken, CancellationToken cancellationToken);

    Task<VideoLibraryResult> ListVideosAsync(
        VideoLibraryListRequest filters,
        string accessToken,
        CancellationToken cancellationToken);

    Task<VideoLibraryResult> RequestVideoTranscriptionAsync(Guid videoId, string idempotencyKey, string accessToken, CancellationToken cancellationToken);

    Task<VideoLibraryResult> ListVideoTagsAsync(string? query, string accessToken, CancellationToken cancellationToken);

    Task<VideoLibraryResult> GetVideoAsync(Guid videoId, string accessToken, CancellationToken cancellationToken);

    Task<VideoLibraryResult> ReplaceVideoTagsAsync(Guid videoId, IReadOnlyList<string> tags, string idempotencyKey, string accessToken, CancellationToken cancellationToken);

    Task<VideoLibraryResult> UpdateVideoTitleAsync(Guid videoId, string title, string idempotencyKey, string accessToken, CancellationToken cancellationToken);
}

public sealed record VideoLibraryResult(
    HttpStatusCode StatusCode,
    string? Code,
    VideoPageResponse? Page,
    VideoResponse? Video,
    VideoTagListResponse? Tags = null);
