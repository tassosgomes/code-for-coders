using CodeForCoders.Notification.Domain.DeliveryRecords;

namespace CodeForCoders.Notification.Domain.Repositories;

public interface IDeliveryRecordRepository
{
    Task AddAsync(DeliveryRecord record, CancellationToken cancellationToken);

    Task<DeliveryRecord?> GetByRequestIdAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken cancellationToken);

    Task<DeliveryRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically selects the next eligible "accepted" record and pushes its
    /// <c>next_attempt_on</c> to <paramref name="leaseUntil"/> in the same statement, so the row
    /// is not picked up again while the caller is delivering it outside of any transaction (G17:
    /// no network call inside a database transaction).
    /// </summary>
    Task<DeliveryRecord?> ClaimNextAcceptedAsync(
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken);
}
