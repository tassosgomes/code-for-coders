using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoQueries(MediaDbContext dbContext) : IVideoQueries
{
    public async Task<VideoPageSnapshot> ListAsync(VideoListQuery filters, CancellationToken cancellationToken)
    {
        var videos = dbContext.Videos.AsNoTracking();
        if (filters.Statuses.Count > 0)
        {
            videos = videos.Where(video => filters.Statuses.Contains(video.Status));
        }

        if (filters.Query is not null)
        {
            var normalizedQuery = Video.NormalizeSearchTitle(filters.Query);
            videos = videos.Where(video => video.NormalizedTitle.Contains(normalizedQuery));
        }
        if (filters.Tags.Count > 0)
        {
            var names = filters.Tags.Select(value => Video.NormalizeSearchTitle(
                string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))))
                .Distinct(StringComparer.Ordinal).ToArray();
            var matchingLinks = from link in dbContext.VideoTags
                                join tag in dbContext.Tags on link.TagId equals tag.TagId
                                where names.Contains(tag.NormalizedName)
                                select link;
            videos = filters.TagMatch == "any"
                ? videos.Where(video => matchingLinks.Any(link => link.VideoId == video.VideoId))
                : videos.Where(video => matchingLinks.Count(link => link.VideoId == video.VideoId) == names.Length);
        }

        var total = await videos.LongCountAsync(cancellationToken);
        var data = await videos
            .OrderByDescending(video => video.UploadedAt)
            .ThenByDescending(video => video.VideoId)
            .Skip((filters.Page - 1) * filters.Size)
            .Take(filters.Size)
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
