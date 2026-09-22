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
    private readonly List<PendingDeliveryOutcomeCounterIncrement> _pendingDeliveryOutcomeCounterIncrements = [];

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<DeliveryRecord> DeliveryRecords => Set<DeliveryRecord>();

    public DbSet<DeliveryOutcomeCounter> DeliveryOutcomeCounters => Set<DeliveryOutcomeCounter>();

    // Increments are staged here instead of going through change tracking so
    // NotificationUnitOfWork.CommitAsync can apply each one as an atomic SQL upsert in the same
    // transaction as the surrounding SaveChangesAsync, instead of a tracked read-then-write that
    // races under concurrent delivery workers (see DeliveryOutcomeCounterRepository).
    internal void EnqueuePendingDeliveryOutcomeCounterIncrement(
        string processingNamespace,
        Guid tenantId,
        string purpose,
        DeliveryStatus status,
        DateOnly outcomeDay)
        => _pendingDeliveryOutcomeCounterIncrements.Add(
            new PendingDeliveryOutcomeCounterIncrement(
                processingNamespace,
                tenantId,
                purpose,
                status,
                outcomeDay));

    internal IReadOnlyList<PendingDeliveryOutcomeCounterIncrement> DequeuePendingDeliveryOutcomeCounterIncrements()
    {
        if (_pendingDeliveryOutcomeCounterIncrements.Count == 0)
        {
            return [];
        }

        var pending = _pendingDeliveryOutcomeCounterIncrements.ToArray();
        _pendingDeliveryOutcomeCounterIncrements.Clear();
        return pending;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(NotificationSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
        // Query filters must keep member access on tenantContext in the expression tree:
        // locals captured here would be frozen when EF caches the model (the first context
        // to build it), and Nullable.Value must not be evaluated while TenantId is unset.
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => message.Namespace == tenantContext.Namespace
                && (tenantContext.TenantId == null || message.TenantId == tenantContext.TenantId));
        modelBuilder.Entity<DeliveryRecord>().HasQueryFilter(
            record => record.Namespace == tenantContext.Namespace
                && (tenantContext.TenantId == null || record.TenantId == tenantContext.TenantId));
        modelBuilder.Entity<DeliveryOutcomeCounter>().HasQueryFilter(
            counter => counter.Namespace == tenantContext.Namespace
                && (tenantContext.TenantId == null || counter.TenantId == tenantContext.TenantId));
    }
}
