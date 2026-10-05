using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed class PlaybackProgressStore(LearningDbContext dbContext, ITenantContext tenantContext,
    ICurrentLessonVideoQueries lessons, TimeProvider timeProvider)
{
    public async Task ApplyAsync(PlaybackProgressFact fact, CancellationToken cancellationToken)
    {
        tenantContext.Set(fact.TenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var receivedAt = timeProvider.GetUtcNow();
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO progress.playback_advances
                (event_id, tenant_id, session_id, student_id, course_id, lesson_id, sequence, position_seconds, reason, occurred_at, received_at)
            VALUES ({fact.EventId}, {fact.TenantId}, {fact.SessionId}, {fact.StudentId}, {fact.CourseId}, {fact.LessonId},
                {fact.Sequence}, {fact.PositionSeconds}, {fact.Reason}, {fact.OccurredAt}, {receivedAt})
            ON CONFLICT (event_id) DO NOTHING
            """, cancellationToken);
        if (inserted == 1)
        {
            var videoId = await lessons.FindVideoAsync(new(fact.CourseId, fact.LessonId), cancellationToken);
            if (videoId.HasValue)
                await ProgressProjectionLock.AcquireAsync(dbContext, $"{fact.TenantId}:{videoId}", cancellationToken);
            var duration = videoId.HasValue ? await dbContext.VideoDurations
                .Where(item => item.VideoId == videoId.Value).Select(item => (int?)item.DurationSeconds)
                .SingleOrDefaultAsync(cancellationToken) : null;
            await UpdateProgressAsync(fact, duration, receivedAt, cancellationToken);
        }
        await transaction.CommitAsync(CancellationToken.None);
    }

    private Task UpdateProgressAsync(PlaybackProgressFact fact, int? duration, DateTimeOffset receivedAt, CancellationToken cancellationToken)
    {
        DateTimeOffset? completedAt = fact.Reason == "ended" || (duration.HasValue && fact.PositionSeconds >= duration.Value * 0.9)
            ? receivedAt : null;
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO progress.lesson_progress
                (tenant_id, student_id, lesson_id, course_id, last_position_seconds, occurred_at, sequence, reason,
                 max_position_seconds, completed_at, last_activity_at)
            VALUES ({fact.TenantId}, {fact.StudentId}, {fact.LessonId}, {fact.CourseId}, {fact.PositionSeconds},
                {fact.OccurredAt}, {fact.Sequence}, {fact.Reason}, {fact.PositionSeconds}, {completedAt}, {fact.OccurredAt})
            ON CONFLICT (tenant_id, student_id, lesson_id) DO UPDATE SET
                last_position_seconds = CASE WHEN (EXCLUDED.occurred_at, EXCLUDED.sequence) > (lesson_progress.occurred_at, lesson_progress.sequence)
                    THEN EXCLUDED.last_position_seconds ELSE lesson_progress.last_position_seconds END,
                reason = CASE WHEN (EXCLUDED.occurred_at, EXCLUDED.sequence) > (lesson_progress.occurred_at, lesson_progress.sequence)
                    THEN EXCLUDED.reason ELSE lesson_progress.reason END,
                sequence = CASE WHEN (EXCLUDED.occurred_at, EXCLUDED.sequence) > (lesson_progress.occurred_at, lesson_progress.sequence)
                    THEN EXCLUDED.sequence ELSE lesson_progress.sequence END,
                occurred_at = GREATEST(lesson_progress.occurred_at, EXCLUDED.occurred_at),
                max_position_seconds = GREATEST(lesson_progress.max_position_seconds, EXCLUDED.max_position_seconds),
                last_activity_at = GREATEST(lesson_progress.last_activity_at, EXCLUDED.last_activity_at),
                completed_at = COALESCE(lesson_progress.completed_at, EXCLUDED.completed_at,
                    CASE WHEN CAST({duration} AS integer) IS NOT NULL AND GREATEST(lesson_progress.max_position_seconds, EXCLUDED.max_position_seconds) >= {duration} * 0.9
                        THEN {receivedAt} ELSE NULL END)
            """, cancellationToken);
    }
}
