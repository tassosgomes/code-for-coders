using CodeForCoders.Notification.Domain.DeliveryRecords;

namespace CodeForCoders.Notification.Domain.Repositories;

public interface IDeliveryOutcomeCounterRepository
{
    Task IncrementAsync(
        DeliveryRecord record,
        DateTimeOffset occurredOn,
        CancellationToken cancellationToken);
}
