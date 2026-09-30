using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed class VideoProjectionStore(LearningDbContext dbContext, TimeProvider timeProvider)
{
    public async Task ApplyAsync(VideoAvailabilityFact fact, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var processedAt = timeProvider.GetUtcNow();
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO content.video_fact_receipts (event_id, tenant_id, processed_at)
            VALUES ({fact.EventId}, {fact.TenantId}, {processedAt}) ON CONFLICT DO NOTHING
            """, cancellationToken);
        if (inserted == 1)
        {
            // Replay and concurrent delivery cannot regress a newer terminal fact.
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO content.projected_videos (tenant_id, video_id, is_ready, occurred_at, event_id)
                VALUES ({fact.TenantId}, {fact.VideoId}, {fact.IsReady}, {fact.OccurredAt}, {fact.EventId})
                ON CONFLICT (tenant_id, video_id) DO UPDATE
                SET is_ready = EXCLUDED.is_ready, occurred_at = EXCLUDED.occurred_at, event_id = EXCLUDED.event_id
                WHERE (EXCLUDED.occurred_at, EXCLUDED.event_id) > (projected_videos.occurred_at, projected_videos.event_id)
                """, cancellationToken);
        }
        await transaction.CommitAsync(CancellationToken.None);
    }
}
