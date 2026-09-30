using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.VideoProjection;

public sealed class ReadyVideoQueries(LearningDbContext dbContext) : IReadyVideoQueries
{
    public Task<bool> IsReadyAsync(Guid videoId, CancellationToken cancellationToken)
        => dbContext.ProjectedVideos.AnyAsync(video => video.VideoId == videoId && video.IsReady, cancellationToken);
}
