using System.Data.Common;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Media.Infra.Data.Transcriptions;

public sealed class VideoTranscriptionRepository(
    MediaDbContext dbContext,
    ITenantContext tenantContext,
    IOptions<TranscriptionOptions> options,
    ILogger<VideoTranscriptionRepository> logger) : IVideoTranscriptionRepository
{
    public async Task WithAutomaticEnqueueAsync(
        Transcription attempt,
        Func<CancellationToken, Task> commitPreparation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        const string savepoint = "automatic_transcription";
        await transaction.CreateSavepointAsync(savepoint, cancellationToken);
        try
        {
            await EnqueueAsync(attempt, cancellationToken);
        }
        catch (DbException)
        {
            // PostgreSQL aborts a transaction on statement failure; preserve the ready/outbox commit.
            await transaction.RollbackToSavepointAsync(savepoint, cancellationToken);
            logger.LogWarning("Automatic transcription could not be queued; video preparation will still complete.");
        }

        await commitPreparation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TranscriptionState>> GetAsync(IReadOnlyList<Video> videos, CancellationToken cancellationToken)
    {
        var ids = videos.Select(video => video.VideoId).ToArray();
        var attempts = options.Value.Enabled && ids.Length > 0
            ? await dbContext.Transcriptions.AsNoTracking().Where(attempt => ids.Contains(attempt.VideoId)).ToArrayAsync(cancellationToken)
            : [];
        var byVideo = attempts.ToLookup(attempt => attempt.VideoId);
        return videos.ToDictionary(video => video.VideoId,
            video => TranscriptionState.Derive(video.Status, byVideo[video.VideoId].ToArray(), options.Value.Enabled));
    }

    public async Task<bool> WithRequestAsync(VideoTranscriptionWriteScope scope, Func<Video, CancellationToken, Task> request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Serialize a replay key even when it targets different videos; the partial index owns queue uniqueness.
        var key = $"requestVideoTranscription:{tenantContext.TenantId}:{scope.ActorAccountId}:{scope.IdempotencyKey}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        var video = await dbContext.Videos.AsNoTracking().SingleOrDefaultAsync(video => video.VideoId == scope.VideoId, cancellationToken);
        if (video is null) return false;
        await request(video, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task EnqueueAsync(Transcription attempt, CancellationToken cancellationToken)
    {
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO media_access.video_transcriptions
                (transcription_id, tenant_id, video_id, origin, requested_by_account_id, status, is_current,
                 extraction_attempts, submissions, submission_failures, requested_at, next_action_at)
            VALUES ({attempt.TranscriptionId}, {attempt.TenantId}, {attempt.VideoId}, {attempt.Origin},
                {attempt.RequestedByAccountId}, {attempt.Status}, false, 0, 0, 0, {attempt.RequestedAt}, {attempt.NextActionAt})
            ON CONFLICT (tenant_id, video_id) WHERE status IN ('queued', 'working', 'waiting') DO NOTHING
            """, cancellationToken);
        // A new attempt replaces the earlier failed one; the current transcript is never touched here (RN-TL07).
        if (inserted == 1)
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                WITH removed AS (
                    DELETE FROM media_access.video_transcriptions
                    WHERE tenant_id = {attempt.TenantId} AND video_id = {attempt.VideoId}
                        AND status = 'failed' AND NOT is_current AND transcription_id <> {attempt.TranscriptionId}
                    RETURNING transcription_id, tenant_id, video_id, requested_at, audio_deleted_at
                )
                INSERT INTO media_access.transcription_audio_cleanups (transcription_id, tenant_id, video_id, requested_at)
                SELECT transcription_id, tenant_id, video_id, requested_at FROM removed WHERE audio_deleted_at IS NULL
                ON CONFLICT (transcription_id) DO NOTHING
                """, cancellationToken);
    }
}
