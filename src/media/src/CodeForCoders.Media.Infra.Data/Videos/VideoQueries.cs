using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoQueries(MediaDbContext dbContext) : IVideoQueries
{
    public async Task<VideoPageSnapshot> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var videos = dbContext.Videos.AsNoTracking();
        var total = await videos.LongCountAsync(cancellationToken);
        var data = await videos
            .OrderByDescending(video => video.UploadedAt)
            .ThenByDescending(video => video.VideoId)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new VideoPageSnapshot(data, total);
    }

    public Task<Video?> GetAsync(Guid videoId, CancellationToken cancellationToken)
        => dbContext.Videos.AsNoTracking().SingleOrDefaultAsync(
            video => video.VideoId == videoId,
            cancellationToken);
}
