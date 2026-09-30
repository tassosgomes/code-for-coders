using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.VideoProjection;

public sealed class ProjectedVideoConfiguration : IEntityTypeConfiguration<ProjectedVideo>
{
    public void Configure(EntityTypeBuilder<ProjectedVideo> builder)
    {
        builder.ToTable("projected_videos", "content");
        builder.HasKey(video => new { video.TenantId, video.VideoId });
        builder.Property(video => video.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(video => video.VideoId).HasColumnName("video_id").ValueGeneratedNever();
        builder.Property(video => video.IsReady).HasColumnName("is_ready");
        builder.Property(video => video.OccurredAt).HasColumnName("occurred_at");
        builder.Property(video => video.EventId).HasColumnName("event_id").ValueGeneratedNever();
    }
}
