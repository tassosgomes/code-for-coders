using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Infra.Data.Configuration;
using CodeForCoders.Billing.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Billing.Infra.Data;

public sealed class BillingDbContext(
    DbContextOptions<BillingDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BillingSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        // Query filters must keep member access on tenantContext in the expression tree:
        // locals captured here would be frozen when EF caches the model (the first context
        // to build it), and Nullable.Value must not be evaluated while TenantId is unset.
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => message.Namespace == tenantContext.Namespace
                && (tenantContext.TenantId == null || message.TenantId == tenantContext.TenantId));
    }
}
