using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoQueries
{
    Task<VideoPageSnapshot> ListAsync(int page, int size, IReadOnlyList<string> statuses, string? query, CancellationToken cancellationToken);

    Task<Video?> GetAsync(Guid videoId, CancellationToken cancellationToken);

    Task<Video?> GetForUpdateAsync(Guid videoId, CancellationToken cancellationToken);
}

public sealed record VideoPageSnapshot(IReadOnlyList<Video> Data, long Total);
