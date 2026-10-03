namespace CodeForCoders.Media.Application.Interfaces;

public interface IPlaybackPlaylistStore
{
    Task<string> ReadAsync(string objectKey, CancellationToken cancellationToken);
}
