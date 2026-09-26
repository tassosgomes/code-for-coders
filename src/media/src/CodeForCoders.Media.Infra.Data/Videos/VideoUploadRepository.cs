using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoUploadRepository(MediaDbContext dbContext) : IVideoUploadRepository
{
    public Task<VideoUpload?> GetOwnedAsync(
        Guid uploadId,
        Guid uploaderAccountId,
        bool includeCompleted,
        CancellationToken cancellationToken)
        => dbContext.VideoUploads.SingleOrDefaultAsync(
            upload => upload.UploadId == uploadId
                && upload.UploaderAccountId == uploaderAccountId
                && (includeCompleted || upload.CompletedAt == null),
            cancellationToken);

    public Task AddAsync(VideoUpload upload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.VideoUploads.Add(upload);
        return Task.CompletedTask;
    }

    public async Task<Video?> GetCompletedVideoAsync(
        Guid uploadId,
        Guid uploaderAccountId,
        CancellationToken cancellationToken)
    {
        var upload = await dbContext.VideoUploads.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.UploadId == uploadId
                && candidate.UploaderAccountId == uploaderAccountId
                && candidate.CompletedAt != null,
            cancellationToken);
        return upload is null
            ? null
            : await dbContext.Videos.SingleOrDefaultAsync(video => video.VideoId == upload.VideoId, cancellationToken);
    }

    public async Task<Video?> CompleteAsync(
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
                && candidate.UploaderAccountId == upload.UploaderAccountId,
            cancellationToken);
        if (current is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await dbContext.Entry(current).ReloadAsync(cancellationToken);
        if (current.CompletedAt is not null)
        {
            var existing = await dbContext.Videos.SingleOrDefaultAsync(
                candidate => candidate.VideoId == current.VideoId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        await completeStorage(current, cancellationToken);
        dbContext.Videos.Add(video);
        current.MarkCompleted(completedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return video;
    }
}
