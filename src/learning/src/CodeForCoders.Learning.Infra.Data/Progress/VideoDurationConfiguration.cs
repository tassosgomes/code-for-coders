using CodeForCoders.Learning.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Progress;

public sealed class VideoDurationConfiguration : IEntityTypeConfiguration<VideoDuration>
{
    public void Configure(EntityTypeBuilder<VideoDuration> builder)
    {
        builder.ToTable("video_durations", LearningSchemas.Progress);
        builder.HasKey(item => new { item.TenantId, item.VideoId });
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.VideoId).HasColumnName("video_id").ValueGeneratedNever();
        builder.Property(item => item.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(item => item.OccurredAt).HasColumnName("occurred_at");
        builder.Property(item => item.EventId).HasColumnName("event_id").ValueGeneratedNever();
    }
}
