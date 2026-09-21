using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using CodeForCoders.BffAdmin.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.BffAdmin.Infra.Data;

public sealed class BffAdminDbContext(
    DbContextOptions<BffAdminDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BffAdminSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BffAdminDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
    }
}
