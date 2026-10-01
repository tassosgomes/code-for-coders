using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class CatalogEditReceiptConfiguration : IEntityTypeConfiguration<CatalogEditReceipt>
{
    public void Configure(EntityTypeBuilder<CatalogEditReceipt> builder)
    {
        builder.ToTable("edit_receipts", CommerceSchemas.Catalog);
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(receipt => receipt.TenantId).HasColumnName("tenant_id");
        builder.Property(receipt => receipt.ActorId).HasColumnName("actor_id");
        builder.Property(receipt => receipt.Key).HasColumnName("key").HasMaxLength(64);
        builder.Property(receipt => receipt.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(receipt => receipt.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(receipt => receipt.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(receipt => new { receipt.TenantId, receipt.ActorId, receipt.Key }).IsUnique();
        builder.HasIndex(receipt => receipt.ExpiresAt);
    }
}
