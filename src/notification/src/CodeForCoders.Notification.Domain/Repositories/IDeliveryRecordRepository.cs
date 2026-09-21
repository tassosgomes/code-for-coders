using CodeForCoders.Notification.Domain.DeliveryRecords;

namespace CodeForCoders.Notification.Domain.Repositories;

public interface IDeliveryRecordRepository
{
    Task AddAsync(DeliveryRecord record, CancellationToken cancellationToken);

    Task<DeliveryRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<DeliveryRecord?> GetNextAcceptedAsync(CancellationToken cancellationToken);
}
