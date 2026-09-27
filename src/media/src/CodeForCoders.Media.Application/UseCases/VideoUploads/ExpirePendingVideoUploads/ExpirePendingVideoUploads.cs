using CodeForCoders.Media.Application.Interfaces;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads.ExpirePendingVideoUploads;

public sealed class ExpirePendingVideoUploads(
    IVideoUploadRepository videoUploads,
    IMediaStoragePort mediaStorage,
    TimeProvider timeProvider) : IExpirePendingVideoUploads
{
    public async Task<int> ExecuteAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "The expiration batch size must be between 1 and 500.");
        }

        var now = timeProvider.GetUtcNow();
        var expiredUploadIds = await videoUploads.GetExpiredPendingIdsAsync(now, batchSize, cancellationToken);
        var expiredCount = 0;
        foreach (var uploadId in expiredUploadIds)
        {
            if (await ExpireAsync(uploadId, now, cancellationToken))
            {
                expiredCount++;
            }
        }

        return expiredCount;
    }

    public Task<bool> ExpireAsync(Guid uploadId, CancellationToken cancellationToken)
        => ExpireAsync(uploadId, timeProvider.GetUtcNow(), cancellationToken);

    private Task<bool> ExpireAsync(Guid uploadId, DateTimeOffset now, CancellationToken cancellationToken)
        => videoUploads.ExpireAsync(
            uploadId,
            now,
            (upload, token) => mediaStorage.AbortMultipartUploadAsync(
                upload.ObjectKey,
                upload.StorageUploadId,
                token),
            cancellationToken);
}
