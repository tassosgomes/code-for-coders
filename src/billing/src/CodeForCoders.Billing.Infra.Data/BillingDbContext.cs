using CodeForCoders.Billing.Domain.Entities;
using CodeForCoders.Billing.Infra.Data.Payments;
using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Infra.Data.Configuration;
using CodeForCoders.Billing.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Billing.Infra.Data;

public sealed class BillingDbContext(
    DbContextOptions<BillingDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<GatewayInboxEntry> GatewayInboxEntries => Set<GatewayInboxEntry>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Payment>().Where(x => x.State == EntityState.Added))
            entry.Property("Namespace").CurrentValue = tenantContext.Namespace;
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BillingSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => EF.Property<string>(x, "Namespace") == tenantContext.Namespace && tenantContext.TenantId.HasValue && x.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<GatewayInboxEntry>().HasQueryFilter(x => x.Namespace == tenantContext.Namespace);
        // Query filters must keep member access on tenantContext in the expression tree:
        // locals captured here would be frozen when EF caches the model (the first context
        // to build it), and Nullable.Value must not be evaluated while TenantId is unset.
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => message.Namespace == tenantContext.Namespace
                && (tenantContext.TenantId == null || message.TenantId == tenantContext.TenantId));
    }
}
