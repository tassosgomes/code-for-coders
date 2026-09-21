using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Infra.Data.Configuration;
using CodeForCoders.Notification.Infra.Data.Outbox;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Notification.Infra.Data;

public sealed class NotificationDbContext(
    DbContextOptions<NotificationDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<DeliveryRecord> DeliveryRecords => Set<DeliveryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(NotificationSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<DeliveryRecord>().HasQueryFilter(
            record => tenantContext.TenantId.HasValue && record.TenantId == tenantContext.TenantId.Value);
    }
}
