using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.ToTable("videos");
        builder.HasKey(video => video.VideoId);

        builder.Property(video => video.VideoId)
            .HasColumnName("video_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(video => video.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(video => video.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(video => video.NormalizedTitle)
            .HasColumnName("normalized_title")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(video => video.UploadedByAccountId)
            .HasColumnName("uploaded_by_account_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(video => video.UploadedByName)
            .HasColumnName("uploaded_by_name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(video => video.UploadedAt)
            .HasColumnName("uploaded_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(video => video.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(video => video.DurationSeconds)
            .HasColumnName("duration_seconds");
        builder.Property(video => video.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(32);

        builder.HasIndex(video => new { video.TenantId, video.UploadedAt, video.VideoId })
            .HasDatabaseName("ix_videos_tenant_uploaded_at");
    }
}
