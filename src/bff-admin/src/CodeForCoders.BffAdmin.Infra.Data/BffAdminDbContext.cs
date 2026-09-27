using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using CodeForCoders.BffAdmin.Infra.Data.Outbox;
using CodeForCoders.BffAdmin.Infra.Data.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.BffAdmin.Infra.Data;

public sealed class BffAdminDbContext(
    DbContextOptions<BffAdminDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AuditComplementIdempotencyRecord> AuditComplementIdempotencyRecords => Set<AuditComplementIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BffAdminSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BffAdminDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<AuditComplementIdempotencyRecord>().HasQueryFilter(
            record => tenantContext.TenantId.HasValue && record.TenantId == tenantContext.TenantId.Value);
    }
}
