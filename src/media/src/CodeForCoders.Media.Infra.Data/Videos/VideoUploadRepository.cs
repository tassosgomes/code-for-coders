using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoUploadRepository(MediaDbContext dbContext) : IVideoUploadRepository
{
    public Task<VideoUpload?> GetOwnedAsync(
        Guid uploadId,
        Guid tenantId,
        Guid uploaderAccountId,
        bool includeCompleted,
        CancellationToken cancellationToken)
        => dbContext.VideoUploads.SingleOrDefaultAsync(
            upload => upload.UploadId == uploadId
                && upload.TenantId == tenantId
                && upload.UploaderAccountId == uploaderAccountId
                && upload.ExpiredAt == null
                && (includeCompleted || upload.CompletedAt == null),
            cancellationToken);

    public Task<VideoUpload?> GetPendingByFingerprintAsync(
        Guid tenantId,
        Guid uploaderAccountId,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => dbContext.VideoUploads.SingleOrDefaultAsync(
            upload => upload.TenantId == tenantId
                && upload.UploaderAccountId == uploaderAccountId
                && upload.Fingerprint == fingerprint
                && upload.CompletedAt == null
                && upload.ExpiredAt == null
                && upload.ExpiresAt > now,
            cancellationToken);

    public Task<VideoUpload?> GetExpiredPendingByFingerprintAsync(
        Guid tenantId,
        Guid uploaderAccountId,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => dbContext.VideoUploads.SingleOrDefaultAsync(
            upload => upload.TenantId == tenantId
                && upload.UploaderAccountId == uploaderAccountId
                && upload.Fingerprint == fingerprint
                && upload.CompletedAt == null
                && upload.ExpiredAt == null
                && upload.ExpiresAt <= now,
            cancellationToken);

    public async Task<(IReadOnlyList<VideoUpload> Uploads, long Total)> ListPendingAsync(
        Guid tenantId,
        Guid uploaderAccountId,
        DateTimeOffset now,
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        var query = dbContext.VideoUploads.AsNoTracking().Where(
            upload => upload.TenantId == tenantId
                && upload.UploaderAccountId == uploaderAccountId
                && upload.CompletedAt == null
                && upload.ExpiredAt == null
                && upload.ExpiresAt > now);
        var total = await query.LongCountAsync(cancellationToken);
        var uploads = await query
            .OrderByDescending(upload => upload.CreatedAt)
            .ThenByDescending(upload => upload.UploadId)
            .Skip((page - 1) * size)
            .Take(size)
            .ToArrayAsync(cancellationToken);
        return (uploads, total);
    }

    public async Task<IReadOnlyList<Guid>> GetExpiredPendingIdsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
        => await dbContext.VideoUploads.IgnoreQueryFilters().AsNoTracking()
            .Where(upload => upload.CompletedAt == null
                && upload.ExpiredAt == null
                && upload.ExpiresAt <= now)
            .OrderBy(upload => upload.ExpiresAt)
            .ThenBy(upload => upload.UploadId)
            .Select(upload => upload.UploadId)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

    public async Task<bool> ExpireAsync(
        Guid uploadId,
        DateTimeOffset now,
        Func<VideoUpload, CancellationToken, Task> abortStorage,
        CancellationToken cancellationToken)
    {
        var candidate = await dbContext.VideoUploads.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(
            upload => upload.UploadId == uploadId,
            cancellationToken);
        if (candidate is null || candidate.CompletedAt is not null || candidate.ExpiredAt is not null || candidate.ExpiresAt > now)
        {
            return false;
        }

        // Storage calls can take seconds. Do not hold the upload row lock while aborting the multipart upload.
        await abortStorage(candidate, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT upload_id FROM media_access.video_uploads WHERE upload_id = {uploadId} FOR UPDATE",
            cancellationToken);

        var upload = await dbContext.VideoUploads.IgnoreQueryFilters().SingleOrDefaultAsync(
            candidate => candidate.UploadId == uploadId,
            cancellationToken);
        if (upload is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        await dbContext.Entry(upload).ReloadAsync(cancellationToken);
        if (upload.CompletedAt is not null || upload.ExpiredAt is not null || upload.ExpiresAt > now)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        upload.MarkExpired(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task AddAsync(VideoUpload upload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.VideoUploads.Add(upload);
        return Task.CompletedTask;
    }

    public async Task<Video?> GetCompletedVideoAsync(
        Guid uploadId,
        Guid tenantId,
        Guid uploaderAccountId,
        CancellationToken cancellationToken)
    {
        var upload = await dbContext.VideoUploads.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.UploadId == uploadId
                && candidate.TenantId == tenantId
                && candidate.UploaderAccountId == uploaderAccountId
                && candidate.ExpiredAt == null
                && candidate.CompletedAt != null,
            cancellationToken);
        return upload is null
            ? null
            : await dbContext.Videos.SingleOrDefaultAsync(video => video.VideoId == upload.VideoId, cancellationToken);
    }

    public async Task<(Video? Video, bool NewlyCompleted)> CompleteAsync(
        VideoUpload upload,
        Video video,
        DateTimeOffset completedAt,
        Func<VideoUpload, CancellationToken, Task> completeStorage,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT upload_id FROM media_access.video_uploads WHERE upload_id = {upload.UploadId} AND tenant_id = {upload.TenantId} AND uploader_account_id = {upload.UploaderAccountId} FOR UPDATE",
            cancellationToken);

        var current = await dbContext.VideoUploads.SingleOrDefaultAsync(
            candidate => candidate.UploadId == upload.UploadId
                && candidate.TenantId == upload.TenantId
                && candidate.UploaderAccountId == upload.UploaderAccountId,
            cancellationToken);
        if (current is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return (null, false);
        }

        await dbContext.Entry(current).ReloadAsync(cancellationToken);
        if (current.ExpiredAt is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return (null, false);
        }

        if (current.CompletedAt is not null)
        {
            var existing = await dbContext.Videos.SingleOrDefaultAsync(
                candidate => candidate.VideoId == current.VideoId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (existing, false);
        }

        await completeStorage(current, cancellationToken);
        dbContext.Videos.Add(video);
        current.MarkCompleted(completedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (video, true);
    }
}
