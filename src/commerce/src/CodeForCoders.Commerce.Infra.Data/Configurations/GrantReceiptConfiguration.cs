using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class GrantReceiptConfiguration : IEntityTypeConfiguration<GrantReceipt>
{
    public void Configure(EntityTypeBuilder<GrantReceipt> builder)
    {
        builder.ToTable("grant_receipts", CommerceSchemas.Entitlement);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.ActorId).HasColumnName("actor_id");
        builder.Property(item => item.KeyHash).HasColumnName("key_hash").HasMaxLength(64);
        builder.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(item => item.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(item => new { item.TenantId, item.ActorId, item.KeyHash }).IsUnique();
        builder.HasIndex(item => item.ExpiresAt);
    }
}
