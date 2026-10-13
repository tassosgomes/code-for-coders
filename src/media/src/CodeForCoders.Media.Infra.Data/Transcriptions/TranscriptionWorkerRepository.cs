using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Transcriptions;

public sealed class TranscriptionWorkerRepository(MediaDbContext dbContext) : ITranscriptionWorkerRepository
{
    public async Task<TranscriptionWorkItem?> ClaimNextAsync(TranscriptionClaim claim, CancellationToken cancellationToken)
    {
        var leaseId = Guid.CreateVersion7(claim.Now);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var candidates = await dbContext.Transcriptions.FromSqlInterpolated($"""
            SELECT * FROM media_access.video_transcriptions
            WHERE status IN ('queued', 'waiting') AND (next_action_at <= {claim.Now} OR signaled_at IS NOT NULL)
                AND ({claim.CanExtract} OR whisper_job_id IS NOT NULL)
            ORDER BY requested_at, transcription_id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).IgnoreQueryFilters().AsNoTracking().ToArrayAsync(cancellationToken);
        if (candidates.Length == 0) return null;
        var id = candidates[0].TranscriptionId;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE media_access.video_transcriptions
            SET status = 'working', signaled_at = NULL, lease_id = {leaseId}, lease_until = {claim.Now.Add(claim.LeaseDuration)},
                extraction_attempts = extraction_attempts + CASE WHEN whisper_job_id IS NULL THEN 1 ELSE 0 END
            WHERE transcription_id = {id}
            """, cancellationToken);
        var attempt = await dbContext.Transcriptions.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.TranscriptionId == id, cancellationToken);
        var video = await dbContext.Videos.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.VideoId == attempt.VideoId && row.TenantId == attempt.TenantId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(attempt, video);
    }

    public async Task<bool> WithLeaseAsync(TranscriptionWorkItem work, Func<Transcription, CancellationToken, Task> change, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var rows = await dbContext.Transcriptions.FromSqlInterpolated($"""
            SELECT * FROM media_access.video_transcriptions
            WHERE transcription_id = {work.Attempt.TranscriptionId} AND status = 'working'
                AND lease_id = {work.Attempt.LeaseId} FOR UPDATE
            """).IgnoreQueryFilters().ToArrayAsync(cancellationToken);
        if (rows.Length == 0) return false;
        await change(rows[0], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RenewLeaseAsync(TranscriptionLeaseRenewal renewal, CancellationToken cancellationToken)
        => await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE media_access.video_transcriptions SET lease_until = {renewal.LeaseUntil}
            WHERE transcription_id = {renewal.TranscriptionId} AND lease_id = {renewal.LeaseId}
                AND status = 'working' AND lease_until > {renewal.Now}
            """, cancellationToken) == 1;

    public async Task MarkCleanupAsync(Guid transcriptionId, DateTimeOffset now, CancellationToken cancellationToken)
        => await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE media_access.video_transcriptions SET audio_deleted_at = {now}
            WHERE transcription_id = {transcriptionId} AND status IN ('ready', 'failed')
            """, cancellationToken);

    public async Task DeleteOldEventsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
        => await dbContext.TranscriptionEvents.IgnoreQueryFilters().Where(row => row.ReceivedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task ReplaceContentAsync(TranscriptionContent content, CancellationToken cancellationToken)
    {
        var attempt = await dbContext.Transcriptions.IgnoreQueryFilters().SingleAsync(row => row.TranscriptionId == content.TranscriptionId, cancellationToken);
        // Release the old partial-index entry before the new current row is saved, inside the lease transaction.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE media_access.video_transcriptions SET is_current = false
            WHERE tenant_id = {content.TenantId} AND video_id = {attempt.VideoId} AND is_current
            """, cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM media_access.video_transcription_contents WHERE transcription_id IN
                (SELECT transcription_id FROM media_access.video_transcriptions
                 WHERE tenant_id = {content.TenantId} AND video_id = {attempt.VideoId} AND transcription_id <> {content.TranscriptionId})
            """, cancellationToken);
        dbContext.TranscriptionContents.Add(content);
    }

    public async Task<IReadOnlyList<TranscriptionWorkItem>> GetExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
        => await ReadWorkAsync(dbContext.Transcriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.Status == "working" && row.LeaseUntil <= now), cancellationToken);

    public async Task<IReadOnlyList<TranscriptionWorkItem>> GetAudioCleanupAsync(CancellationToken cancellationToken)
        => await ReadWorkAsync(dbContext.Transcriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.AudioDeletedAt == null && (row.Status == "ready" || row.Status == "failed")), cancellationToken);

    private async Task<IReadOnlyList<TranscriptionWorkItem>> ReadWorkAsync(IQueryable<Transcription> query, CancellationToken cancellationToken)
        => await query.OrderBy(row => row.RequestedAt).Take(100)
            .Join(dbContext.Videos.IgnoreQueryFilters().AsNoTracking(), row => row.VideoId, video => video.VideoId,
                (row, video) => new TranscriptionWorkItem(row, video)).ToArrayAsync(cancellationToken);
}
