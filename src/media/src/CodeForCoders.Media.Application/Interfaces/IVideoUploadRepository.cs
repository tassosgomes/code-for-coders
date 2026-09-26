using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoUploadRepository
{
    Task<VideoUpload?> GetOwnedAsync(
        Guid uploadId,
        Guid tenantId,
        Guid uploaderAccountId,
        bool includeCompleted,
        CancellationToken cancellationToken);

    Task<VideoUpload?> GetPendingByFingerprintAsync(
        Guid tenantId,
        Guid uploaderAccountId,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<VideoUpload?> GetExpiredPendingByFingerprintAsync(
        Guid tenantId,
        Guid uploaderAccountId,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<VideoUpload> Uploads, long Total)> ListPendingAsync(
        Guid tenantId,
        Guid uploaderAccountId,
        DateTimeOffset now,
        int page,
        int size,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetExpiredPendingIdsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> ExpireAsync(
        Guid uploadId,
        DateTimeOffset now,
        Func<VideoUpload, CancellationToken, Task> abortStorage,
        CancellationToken cancellationToken);

    Task AddAsync(VideoUpload upload, CancellationToken cancellationToken);

    Task<Video?> GetCompletedVideoAsync(
        Guid uploadId,
        Guid tenantId,
        Guid uploaderAccountId,
        CancellationToken cancellationToken);

    Task<Video?> CompleteAsync(
        VideoUpload upload,
        Video video,
        DateTimeOffset completedAt,
        Func<VideoUpload, CancellationToken, Task> completeStorage,
        CancellationToken cancellationToken);
}
