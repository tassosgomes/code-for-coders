using System.Net;
using CodeForCoders.BffAdmin.Api.ApiModels;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IVideoLibraryClient
{
    Task<VideoLibraryResult> ListVideosAsync(
        int page,
        int size,
        string accessToken,
        CancellationToken cancellationToken);

    Task<VideoLibraryResult> GetVideoAsync(Guid videoId, string accessToken, CancellationToken cancellationToken);
}

public sealed record VideoLibraryResult(
    HttpStatusCode StatusCode,
    string? Code,
    VideoPageResponse? Page,
    VideoResponse? Video);
