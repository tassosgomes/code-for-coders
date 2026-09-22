using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Domain.Repositories;

namespace CodeForCoders.Notification.Infra.Data.Repositories;

public sealed class DeliveryOutcomeCounterRepository(NotificationDbContext dbContext)
    : IDeliveryOutcomeCounterRepository
{
    public Task IncrementAsync(
        DeliveryRecord record,
        DateTimeOffset occurredOn,
        CancellationToken cancellationToken)
    {
        var purpose = string.IsNullOrWhiteSpace(record.Purpose)
            ? DeliveryOutcomeCounter.UnspecifiedPurpose
            : record.Purpose;
        var outcomeDay = DateOnly.FromDateTime(occurredOn.UtcDateTime);

        // Validates tenant id, status and purpose length with the same invariants a freshly
        // created counter enforces; the instance itself is discarded. Persistence is deferred to
        // NotificationUnitOfWork.CommitAsync, which applies an atomic SQL upsert instead of a
        // tracked read-then-write, so two concurrent delivery workers incrementing the same
        // tenant/purpose/status/day never lose an update or fail on a primary-key violation.
        DeliveryOutcomeCounter.Create(
            record.Namespace,
            record.TenantId,
            purpose,
            record.Status,
            outcomeDay);

        dbContext.EnqueuePendingDeliveryOutcomeCounterIncrement(
            record.Namespace,
            record.TenantId,
            purpose,
            record.Status,
            outcomeDay);

        return Task.CompletedTask;
    }
}
