namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoPreparationWorkflow
{
    Task<bool> ExecuteNextAsync(
        string workDirectory,
        long availableDiskBytes,
        TimeSpan leaseDuration,
        TimeSpan leaseRenewalInterval,
        CancellationToken cancellationToken);

    Task<int> RecoverExpiredLeasesAsync(int batchSize, CancellationToken cancellationToken);

    Task<int> CleanupReadyOriginalsAsync(int batchSize, CancellationToken cancellationToken);
}
