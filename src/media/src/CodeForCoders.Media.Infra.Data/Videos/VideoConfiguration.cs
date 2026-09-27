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
        builder.Property(video => video.OriginalObjectKey)
            .HasColumnName("original_object_key")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(video => video.OriginalSizeBytes)
            .HasColumnName("original_size_bytes")
            .IsRequired();
        builder.Property(video => video.StoredBytes)
            .HasColumnName("stored_bytes")
            .IsRequired();
        builder.Property(video => video.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(512);
        builder.Property(video => video.PreparationAttempts)
            .HasColumnName("preparation_attempts")
            .IsRequired();
        builder.Property(video => video.NextPreparationAt)
            .HasColumnName("next_preparation_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(video => video.PreparationLeaseId)
            .HasColumnName("preparation_lease_id")
            .HasColumnType("uuid")
            .IsConcurrencyToken();
        builder.Property(video => video.PreparationLeaseUntil)
            .HasColumnName("preparation_lease_until")
            .HasColumnType("timestamp with time zone");
        builder.Property(video => video.EncryptedVideoKey)
            .HasColumnName("encrypted_video_key")
            .HasColumnType("bytea");
        builder.Property(video => video.MasterKeyId)
            .HasColumnName("master_key_id")
            .HasMaxLength(64);
        builder.Property(video => video.OriginalDeletedAt)
            .HasColumnName("original_deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(video => new { video.TenantId, video.UploadedAt, video.VideoId })
            .HasDatabaseName("ix_videos_tenant_uploaded_at");
        builder.HasIndex(video => new { video.Status, video.NextPreparationAt, video.UploadedAt, video.VideoId })
            .HasDatabaseName("ix_videos_preparation_queue")
            .HasFilter("status = 'received'");
        builder.HasIndex(video => new { video.Status, video.PreparationLeaseUntil })
            .HasDatabaseName("ix_videos_preparation_lease")
            .HasFilter("status = 'preparing'");
        builder.HasIndex(video => new { video.Status, video.OriginalDeletedAt })
            .HasDatabaseName("ix_videos_original_cleanup")
            .HasFilter("status IN ('ready', 'failed') AND original_deleted_at IS NULL");
    }
}
