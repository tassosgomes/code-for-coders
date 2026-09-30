using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.VideoProjection;

public sealed class VideoProjectionReconciler(LearningDbContext dbContext)
{
    public async Task<bool> MatchesAsync(Guid tenantId, IReadOnlySet<Guid> mediaReadyIds, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        var projected = await dbContext.ProjectedVideos.IgnoreQueryFilters().AsNoTracking()
            .Where(video => video.TenantId == tenantId && video.IsReady).Select(video => video.VideoId).ToListAsync(cancellationToken);
        return mediaReadyIds.SetEquals(projected);
    }
}
