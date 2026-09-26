using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoUploadRepository
{
    Task<VideoUpload?> GetOwnedAsync(
        Guid uploadId,
        Guid uploaderAccountId,
        bool includeCompleted,
        CancellationToken cancellationToken);

    Task AddAsync(VideoUpload upload, CancellationToken cancellationToken);

    Task<Video?> GetCompletedVideoAsync(
        Guid uploadId,
        Guid uploaderAccountId,
        CancellationToken cancellationToken);

    Task<Video?> CompleteAsync(
        VideoUpload upload,
        Video video,
        DateTimeOffset completedAt,
        Func<VideoUpload, CancellationToken, Task> completeStorage,
        CancellationToken cancellationToken);
}
