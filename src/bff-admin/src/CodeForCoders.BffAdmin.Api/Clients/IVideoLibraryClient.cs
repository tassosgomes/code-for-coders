using System.Net;
using CodeForCoders.BffAdmin.Api.ApiModels;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IVideoLibraryClient
{
    Task<VideoLibraryResult> ListVideosAsync(
        int page,
        int size,
        IReadOnlyList<string> statuses,
        string? query,
        string accessToken,
        CancellationToken cancellationToken);

    Task<VideoLibraryResult> GetVideoAsync(Guid videoId, string accessToken, CancellationToken cancellationToken);

    Task<VideoLibraryResult> UpdateVideoTitleAsync(Guid videoId, string title, string idempotencyKey, string accessToken, CancellationToken cancellationToken);
}

public sealed record VideoLibraryResult(
    HttpStatusCode StatusCode,
    string? Code,
    VideoPageResponse? Page,
    VideoResponse? Video);
