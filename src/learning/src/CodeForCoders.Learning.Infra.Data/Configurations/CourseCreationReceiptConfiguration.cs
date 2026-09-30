using CodeForCoders.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Configurations;

public sealed class CourseCreationReceiptConfiguration : IEntityTypeConfiguration<CourseCreationReceipt>
{
    public void Configure(EntityTypeBuilder<CourseCreationReceipt> builder)
    {
        builder.ToTable("course_creation_receipts", "content");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(receipt => receipt.TenantId).HasColumnName("tenant_id");
        builder.Property(receipt => receipt.ActorId).HasColumnName("actor_id");
        builder.Property(receipt => receipt.Key).HasColumnName("key").HasMaxLength(128);
        builder.Property(receipt => receipt.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(receipt => receipt.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(receipt => receipt.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(receipt => new { receipt.TenantId, receipt.ActorId, receipt.Key }).IsUnique();
    }
}
