namespace CodeForCoders.Media.Application.Interfaces;

public interface IPlaybackPlaylistReader
{
    Task<string> ReadPlaylistAsync(string objectKey, CancellationToken cancellationToken);
}
