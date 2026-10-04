using CodeForCoders.Media.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CodeForCoders.Media.Infra.Data.Adapters;

public sealed class PlaybackPlaylistStore(IPlaybackPlaylistReader storage, PlaybackPlaylistCache cache) : IPlaybackPlaylistStore
{

    public async Task<string> ReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        if (cache.Originals.TryGetValue(objectKey, out string? cached) && cached is not null) return cached;
        var original = await storage.ReadPlaylistAsync(objectKey, cancellationToken);
        cache.Originals.Set(objectKey, original, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromHours(1) });
        return original;
    }
}
