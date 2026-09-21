using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using CodeForCoders.Notification.Infra.Data.Configuration;
using Npgsql;

namespace CodeForCoders.Notification.Infra.Data.Repositories;

public sealed class DeliveryRecordRepository(NotificationDbContext dbContext) : IDeliveryRecordRepository
{
    public Task AddAsync(DeliveryRecord record, CancellationToken cancellationToken)
    {
        dbContext.DeliveryRecords.Add(record);
        return Task.CompletedTask;
    }

    public Task<DeliveryRecord?> GetByRequestIdAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken cancellationToken)
        => dbContext.DeliveryRecords
            .SingleOrDefaultAsync(
                record => record.TenantId == tenantId && record.RequestId == requestId,
                cancellationToken);

    public Task<DeliveryRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.DeliveryRecords.SingleOrDefaultAsync(record => record.Id == id, cancellationToken);

    public async Task<DeliveryRecord?> ClaimNextAcceptedAsync(
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        CancellationToken cancellationToken)
    {
        // The claim (selection + lease) is a single atomic statement, so it needs no explicit
        // transaction of its own and never spans the outbound call to the email provider: the
        // caller performs that call afterward, with the scope's transaction already closed.
        // An UPDATE ... RETURNING statement is not composable, so the result is materialized with
        // ToListAsync instead of SingleOrDefaultAsync, which would make EF Core try to wrap it in
        // a subquery; the WHERE id = (... LIMIT 1) subquery already guarantees at most one row.
        var claimed = await dbContext.DeliveryRecords
            .FromSqlRaw($"""
                UPDATE {NotificationSchema.Name}.delivery_records
                SET next_attempt_on = @leaseUntil
                WHERE id = (
                    SELECT id FROM {NotificationSchema.Name}.delivery_records
                    WHERE status = 'accepted'
                      AND (next_attempt_on IS NULL OR next_attempt_on <= @now)
                    ORDER BY accepted_on, id
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *
                """,
                new NpgsqlParameter("leaseUntil", leaseUntil),
                new NpgsqlParameter("now", now))
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
        return claimed.SingleOrDefault();
    }
}
