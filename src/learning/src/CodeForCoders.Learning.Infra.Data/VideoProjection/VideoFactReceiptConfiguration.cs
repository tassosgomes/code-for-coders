using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.VideoProjection;

public sealed class VideoFactReceiptConfiguration : IEntityTypeConfiguration<VideoFactReceipt>
{
    public void Configure(EntityTypeBuilder<VideoFactReceipt> builder)
    {
        builder.ToTable("video_fact_receipts", "content");
        builder.HasKey(receipt => receipt.EventId);
        builder.Property(receipt => receipt.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(receipt => receipt.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(receipt => receipt.ProcessedAt).HasColumnName("processed_at");
    }
}
