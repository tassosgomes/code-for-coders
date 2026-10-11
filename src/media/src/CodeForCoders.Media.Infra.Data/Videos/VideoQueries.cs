using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoQueries(MediaDbContext dbContext) : IVideoQueries
{
    public async Task<VideoPageSnapshot> ListAsync(int page, int size, IReadOnlyList<string> statuses, string? query, CancellationToken cancellationToken)
    {
        var videos = dbContext.Videos.AsNoTracking();
        if (statuses.Count > 0)
        {
            videos = videos.Where(video => statuses.Contains(video.Status));
        }

        if (query is not null)
        {
            var normalizedQuery = Video.NormalizeSearchTitle(query);
            videos = videos.Where(video => video.NormalizedTitle.Contains(normalizedQuery));
        }
        var total = await videos.LongCountAsync(cancellationToken);
        var data = await videos
            .OrderByDescending(video => video.UploadedAt)
            .ThenByDescending(video => video.VideoId)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new VideoPageSnapshot(data, total);
    }

    public async Task<IReadOnlyList<string>> ListTagsAsync(string? query, CancellationToken cancellationToken)
    {
        const int suggestionLimit = 20;
        var normalizedQuery = query is null ? string.Empty : Video.NormalizeSearchTitle(query);
        var tags = dbContext.Tags.AsNoTracking()
            .Where(tag => dbContext.VideoTags.Any(link => link.TagId == tag.TagId));
        if (query is not null)
            tags = tags.Where(tag => tag.NormalizedName.Contains(normalizedQuery));
        return await tags.OrderByDescending(tag => tag.NormalizedName.StartsWith(normalizedQuery))
            .ThenBy(tag => tag.NormalizedName).ThenBy(tag => tag.TagId)
            .Select(tag => tag.DisplayName).Take(suggestionLimit).ToArrayAsync(cancellationToken);
    }

    public Task<Video?> GetAsync(Guid videoId, CancellationToken cancellationToken)
        => dbContext.Videos.AsNoTracking().SingleOrDefaultAsync(
            video => video.VideoId == videoId,
            cancellationToken);

    public Task<Video?> GetForUpdateAsync(Guid videoId, CancellationToken cancellationToken)
        => dbContext.Videos.SingleOrDefaultAsync(video => video.VideoId == videoId, cancellationToken);
}
