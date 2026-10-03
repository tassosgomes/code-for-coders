using Microsoft.Extensions.Caching.Memory;

namespace CodeForCoders.Media.Infra.Data.Adapters;

public sealed class PlaybackPlaylistCache : IDisposable
{
    public MemoryCache Originals { get; } = new(new MemoryCacheOptions { SizeLimit = 64 });
    public void Dispose() => Originals.Dispose();
}
