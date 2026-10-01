using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class PurchaseIntentDailyCountConfiguration : IEntityTypeConfiguration<PurchaseIntentDailyCount>
{
    public void Configure(EntityTypeBuilder<PurchaseIntentDailyCount> builder)
    {
        builder.ToTable("purchase_intent_daily_counts", "catalog");
        builder.HasKey(item => new { item.TenantId, item.OfferId, item.Day });
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.OfferId).HasColumnName("offer_id");
        builder.Property(item => item.Day).HasColumnName("day");
        builder.Property(item => item.Count).HasColumnName("count");
    }
}
