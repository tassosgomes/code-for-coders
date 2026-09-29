using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data.Common;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoPreparationRepository(MediaDbContext dbContext) : IVideoPreparationRepository
{
    public async Task<VideoPreparationLease?> ClaimNextAsync(
        DateTimeOffset now,
        TimeSpan leaseDuration,
        long availableDiskBytes,
        CancellationToken cancellationToken)
    {
        if (availableDiskBytes < 3 || leaseDuration <= TimeSpan.Zero)
        {
            return null;
        }

        var leaseId = Guid.CreateVersion7(now);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"""
                WITH candidate AS (
                    SELECT video.video_id,
                           video.original_object_key,
                           video.original_size_bytes
                    FROM {MediaSchema.Name}.videos AS video
                    WHERE video.status = 'received'
                      AND video.preparation_attempts < {Video.MaximumPreparationAttempts}
                      AND (video.next_preparation_at IS NULL OR video.next_preparation_at <= @now)
                      AND video.original_object_key <> ''
                      AND video.original_size_bytes > 0
                      AND video.original_size_bytes <= @maximum_original_size
                    ORDER BY video.uploaded_at, video.video_id
                    LIMIT 1
                    FOR UPDATE OF video SKIP LOCKED
                )
                UPDATE {MediaSchema.Name}.videos AS video
                SET status = 'preparing',
                    preparation_lease_id = @lease_id,
                    preparation_lease_until = @lease_until,
                    original_object_key = candidate.original_object_key,
                    original_size_bytes = candidate.original_size_bytes,
                    next_preparation_at = NULL,
                    preparation_attempts = preparation_attempts + 1
                FROM candidate
                WHERE video.video_id = candidate.video_id
                RETURNING video.video_id, video.original_object_key, video.original_size_bytes,
                          video.correlation_id, video.uploaded_at, video.preparation_attempts
                """;
            AddParameter(command, "now", now);
            AddParameter(command, "maximum_original_size", availableDiskBytes / 3);
            AddParameter(command, "lease_id", leaseId);
            AddParameter(command, "lease_until", now.Add(leaseDuration));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new VideoPreparationLease(
                reader.GetGuid(0),
                leaseId,
                reader.GetString(1),
                reader.GetInt64(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetInt32(5));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public Task<Video?> GetClaimedAsync(Guid videoId, Guid leaseId, CancellationToken cancellationToken)
        => dbContext.Videos.IgnoreQueryFilters().SingleOrDefaultAsync(
            video => video.VideoId == videoId
                && video.Status == "preparing"
                && video.PreparationLeaseId == leaseId,
            cancellationToken);

    public async Task<bool> RenewLeaseAsync(
        Guid videoId,
        Guid leaseId,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken)
    {
        var command = $"""
            UPDATE {MediaSchema.Name}.videos
            SET preparation_lease_until = @leaseUntil
            WHERE video_id = @videoId
              AND status = 'preparing'
              AND preparation_lease_id = @leaseId
            """;
        var changed = await dbContext.Database.ExecuteSqlRawAsync(
            command,
            [
                new NpgsqlParameter("leaseUntil", leaseUntil),
                new NpgsqlParameter("videoId", videoId),
                new NpgsqlParameter("leaseId", leaseId),
            ],
            cancellationToken);
        return changed == 1;
    }

    public async Task<IReadOnlyList<VideoPreparationLease>> GetExpiredLeasesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
        => (await dbContext.Videos.IgnoreQueryFilters().AsNoTracking()
            .Where(video => video.Status == "preparing"
                && video.PreparationLeaseId != null
                && video.PreparationLeaseUntil <= now)
            .OrderBy(video => video.PreparationLeaseUntil)
            .ThenBy(video => video.VideoId)
            .Take(batchSize)
            .Select(video => new
            {
                video.VideoId,
                video.PreparationLeaseId,
                video.OriginalObjectKey,
                video.OriginalSizeBytes,
                video.CorrelationId,
                video.UploadedAt,
                video.PreparationAttempts,
            })
            .ToArrayAsync(cancellationToken))
            .Select(video => new VideoPreparationLease(
                video.VideoId,
                video.PreparationLeaseId!.Value,
                video.OriginalObjectKey,
                video.OriginalSizeBytes,
                video.CorrelationId,
                video.UploadedAt,
                video.PreparationAttempts))
            .ToArray();

    public async Task<IReadOnlyList<VideoOriginalCleanup>> GetFinalOriginalsForCleanupAsync(
        int batchSize,
        CancellationToken cancellationToken)
        => await dbContext.Videos.IgnoreQueryFilters().AsNoTracking()
            .Where(video => (video.Status == "ready" || video.Status == "failed")
                && video.OriginalDeletedAt == null)
            .OrderBy(video => video.UploadedAt)
            .ThenBy(video => video.VideoId)
            .Take(batchSize)
            .Select(video => new VideoOriginalCleanup(video.VideoId, video.OriginalObjectKey, video.Status))
            .ToArrayAsync(cancellationToken);

    public async Task<bool> MarkOriginalDeletedAsync(
        Guid videoId,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken)
    {
        var video = await dbContext.Videos.IgnoreQueryFilters().SingleOrDefaultAsync(
            candidate => candidate.VideoId == videoId,
            cancellationToken);
        return video is not null && video.MarkOriginalDeleted(deletedAt);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
