using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

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

    public Task<DeliveryRecord?> GetNextAcceptedAsync(CancellationToken cancellationToken)
        => dbContext.DeliveryRecords
            .IgnoreQueryFilters()
            .Where(record => record.Status == DeliveryStatus.Accepted)
            .OrderBy(record => record.AcceptedOn)
            .ThenBy(record => record.Id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
}
