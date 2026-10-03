namespace CodeForCoders.BffStudent.Api.Clients;

public interface IPlaybackMediaClient
{
    Task<PlaybackProxyResult> SendAsync(HttpMethod method, string path, string accessToken, CancellationToken cancellationToken);
}
