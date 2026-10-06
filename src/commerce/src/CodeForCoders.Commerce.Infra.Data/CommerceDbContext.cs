using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data;

public sealed class CommerceDbContext(
    DbContextOptions<CommerceDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();
    public DbSet<GrantReceipt> GrantReceipts => Set<GrantReceipt>();
    public DbSet<EntitlementOutboxMessage> EntitlementOutboxMessages => Set<EntitlementOutboxMessage>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<CatalogOutboxMessage> CatalogOutboxMessages => Set<CatalogOutboxMessage>();

    public DbSet<EntitlementCourseView> EntitlementCourseViews => Set<EntitlementCourseView>();

    public DbSet<CatalogCourseView> CatalogCourseViews => Set<CatalogCourseView>();
    public DbSet<CatalogOffer> CatalogOffers => Set<CatalogOffer>();
    public DbSet<CatalogEditReceipt> CatalogEditReceipts => Set<CatalogEditReceipt>();

    public DbSet<PurchaseIntentDailyCount> PurchaseIntentDailyCounts => Set<PurchaseIntentDailyCount>();
    public DbSet<PurchaseIntentReceipt> PurchaseIntentReceipts => Set<PurchaseIntentReceipt>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderReceipt> OrderReceipts => Set<OrderReceipt>();
    public DbSet<OrderSequence> OrderSequences => Set<OrderSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OrderReceipt>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OrderSequence>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<Enrollment>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<AccessGrant>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<GrantReceipt>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<EntitlementOutboxMessage>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.HasDefaultSchema(CommerceSchemas.Catalog);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);
        modelBuilder.Entity<PurchaseIntentDailyCount>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<PurchaseIntentReceipt>().HasQueryFilter(item => tenantContext.TenantId.HasValue && item.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<EntitlementCourseView>().HasQueryFilter(
            course => tenantContext.TenantId.HasValue && course.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CatalogCourseView>().HasQueryFilter(
            course => tenantContext.TenantId.HasValue && course.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CatalogOffer>().HasQueryFilter(
            offer => tenantContext.TenantId.HasValue && offer.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CatalogEditReceipt>().HasQueryFilter(
            receipt => tenantContext.TenantId.HasValue && receipt.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CatalogOutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
    }
}
