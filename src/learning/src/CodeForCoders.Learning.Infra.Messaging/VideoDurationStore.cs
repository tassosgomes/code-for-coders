using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed class VideoDurationStore(LearningDbContext dbContext, ITenantContext tenantContext,
    ICurrentLessonVideoQueries lessons, TimeProvider timeProvider)
{
    // The caller owns the transaction shared with Content's receipt and availability projection.
    public async Task ApplyAsync(VideoAvailabilityFact fact, CancellationToken cancellationToken)
    {
        if (!fact.IsReady || !fact.DurationSeconds.HasValue) return;
        tenantContext.Set(fact.TenantId);
        await ProgressProjectionLock.AcquireAsync(dbContext, $"{fact.TenantId}:{fact.VideoId}", cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO progress.video_durations (tenant_id, video_id, duration_seconds, occurred_at, event_id)
            VALUES ({fact.TenantId}, {fact.VideoId}, {fact.DurationSeconds.Value}, {fact.OccurredAt}, {fact.EventId})
            ON CONFLICT (tenant_id, video_id) DO UPDATE
            SET duration_seconds = EXCLUDED.duration_seconds, occurred_at = EXCLUDED.occurred_at, event_id = EXCLUDED.event_id
            WHERE (EXCLUDED.occurred_at, EXCLUDED.event_id) > (video_durations.occurred_at, video_durations.event_id)
            """, cancellationToken);
        var duration = await dbContext.VideoDurations.Where(item => item.VideoId == fact.VideoId)
            .Select(item => item.DurationSeconds).SingleAsync(cancellationToken);
        var currentLessons = await lessons.ListByVideoAsync(fact.VideoId, cancellationToken);
        var completedAt = timeProvider.GetUtcNow();
        foreach (var lesson in currentLessons)
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE progress.lesson_progress SET completed_at = {completedAt}
                WHERE tenant_id = {fact.TenantId} AND course_id = {lesson.CourseId} AND lesson_id = {lesson.LessonId}
                  AND completed_at IS NULL AND max_position_seconds >= {duration} * 0.9
                """, cancellationToken);
    }
}
