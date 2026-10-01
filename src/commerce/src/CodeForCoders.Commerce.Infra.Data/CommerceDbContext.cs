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
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<CatalogCourseView> CatalogCourseViews => Set<CatalogCourseView>();
    public DbSet<CatalogOffer> CatalogOffers => Set<CatalogOffer>();
    public DbSet<CatalogEditReceipt> CatalogEditReceipts => Set<CatalogEditReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(CommerceSchemas.Catalog);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);
        modelBuilder.Entity<CatalogCourseView>().HasQueryFilter(
            course => tenantContext.TenantId.HasValue && course.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CatalogOffer>().HasQueryFilter(
            offer => tenantContext.TenantId.HasValue && offer.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CatalogEditReceipt>().HasQueryFilter(
            receipt => tenantContext.TenantId.HasValue && receipt.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
    }
}
