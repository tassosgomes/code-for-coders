using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoPreparationRepository
{
    Task<VideoPreparationLease?> ClaimNextAsync(
        DateTimeOffset now,
        TimeSpan leaseDuration,
        long availableDiskBytes,
        CancellationToken cancellationToken);

    Task<Video?> GetClaimedAsync(Guid videoId, Guid leaseId, CancellationToken cancellationToken);

    Task<bool> RenewLeaseAsync(
        Guid videoId,
        Guid leaseId,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VideoPreparationLease>> GetExpiredLeasesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VideoOriginalCleanup>> GetFinalOriginalsForCleanupAsync(
        int batchSize,
        CancellationToken cancellationToken);

    Task<bool> MarkOriginalDeletedAsync(Guid videoId, DateTimeOffset deletedAt, CancellationToken cancellationToken);
}

public sealed record VideoPreparationLease(
    Guid VideoId,
    Guid LeaseId,
    string OriginalObjectKey,
    long OriginalSizeBytes,
    string? CorrelationId,
    DateTimeOffset UploadedAt,
    int PreparationAttempts);

public sealed record VideoOriginalCleanup(Guid VideoId, string OriginalObjectKey, string Status);
