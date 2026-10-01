using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class PurchaseIntentReceiptConfiguration : IEntityTypeConfiguration<PurchaseIntentReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseIntentReceipt> builder)
    {
        builder.ToTable("purchase_intent_receipts", "catalog");
        builder.HasKey(item => new { item.TenantId, item.OfferId, item.KeyHash });
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.OfferId).HasColumnName("offer_id");
        builder.Property(item => item.KeyHash).HasColumnName("key_hash").HasMaxLength(64);
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(item => item.ExpiresAt);
    }
}
