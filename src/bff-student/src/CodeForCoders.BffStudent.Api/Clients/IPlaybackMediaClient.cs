namespace CodeForCoders.BffStudent.Api.Clients;

public interface IPlaybackMediaClient
{
    Task<PlaybackProxyResult> SendAsync(HttpMethod method, string path, string accessToken, CancellationToken cancellationToken);
    Task<PlaybackProxyResult> SendAsync(HttpMethod method, string path, string accessToken, HttpContent? content, TimeSpan? timeout, CancellationToken cancellationToken);
}
