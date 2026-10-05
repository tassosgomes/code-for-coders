using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class OrderReceiptConfiguration : IEntityTypeConfiguration<OrderReceipt>
{
    public void Configure(EntityTypeBuilder<OrderReceipt> builder)
    {
        builder.ToTable("order_receipts", CommerceSchemas.Sales);
        builder.HasKey(item => new { item.TenantId, item.StudentId, item.KeyHash });
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.StudentId).HasColumnName("student_id");
        builder.Property(item => item.KeyHash).HasColumnName("key_hash").HasMaxLength(64);
        builder.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(item => item.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(item => item.StatusCode).HasColumnName("status_code");
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(item => item.ExpiresAt);
    }
}
