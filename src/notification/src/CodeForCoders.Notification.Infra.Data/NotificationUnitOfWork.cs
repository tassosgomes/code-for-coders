using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Notification.Infra.Data;

public sealed class NotificationUnitOfWork(NotificationDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var pendingIncrements = dbContext.DequeuePendingDeliveryOutcomeCounterIncrements();
        if (pendingIncrements.Count == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Each staged increment is applied here as an atomic INSERT ... ON CONFLICT upsert, in
        // the same transaction as the SaveChangesAsync below, so a delivery-record transition and
        // its aggregate counter increment land in the same commit (task 7.0) while staying immune
        // to the lost-update/unique-violation race a tracked read-then-write would hit under
        // concurrent delivery workers (see DeliveryOutcomeCounterRepository.IncrementAsync).
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // The schema is an identifier and cannot be sent as a database parameter. It is a
        // compile-time constant, so interpolating it directly is safe (same pattern as
        // OutboxPublisherWorker); the {0}-{3} placeholders below are still bound as real
        // parameters by ExecuteSqlRawAsync.
        var sql = $"INSERT INTO {NotificationSchema.Name}.delivery_outcome_counters "
            + "(namespace, tenant_id, purpose, status, outcome_day, count) "
            + "VALUES ({0}, {1}, {2}, {3}, {4}, 1) "
            + "ON CONFLICT (namespace, tenant_id, purpose, status, outcome_day) "
            + "DO UPDATE SET count = delivery_outcome_counters.count + 1";
        foreach (var increment in pendingIncrements)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                sql,
                new object[]
                {
                    increment.Namespace,
                    increment.TenantId,
                    increment.Purpose,
                    increment.Status.ToString().ToLowerInvariant(),
                    increment.OutcomeDay,
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
