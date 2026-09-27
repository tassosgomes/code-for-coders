using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoUploadConfiguration : IEntityTypeConfiguration<VideoUpload>
{
    public void Configure(EntityTypeBuilder<VideoUpload> builder)
    {
        builder.ToTable("video_uploads");
        builder.HasKey(upload => upload.UploadId);

        builder.Property(upload => upload.UploadId)
            .HasColumnName("upload_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(upload => upload.VideoId)
            .HasColumnName("video_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(upload => upload.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(upload => upload.UploaderAccountId)
            .HasColumnName("uploader_account_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(upload => upload.UploaderName)
            .HasColumnName("uploader_name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(upload => upload.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(upload => upload.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(upload => upload.FileSize)
            .HasColumnName("file_size")
            .IsRequired();
        builder.Property(upload => upload.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(upload => upload.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(upload => upload.ObjectKey)
            .HasColumnName("object_key")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(upload => upload.StorageUploadId)
            .HasColumnName("storage_upload_id")
            .HasMaxLength(1024)
            .IsRequired();
        builder.Property(upload => upload.PartCount)
            .HasColumnName("part_count")
            .IsRequired();
        builder.Property(upload => upload.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(upload => upload.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(upload => upload.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(upload => upload.ExpiredAt)
            .HasColumnName("expired_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(upload => new { upload.TenantId, upload.UploaderAccountId, upload.CreatedAt })
            .HasDatabaseName("ix_video_uploads_tenant_actor_created_at");
        builder.HasIndex(upload => new { upload.TenantId, upload.UploaderAccountId, upload.Fingerprint })
            .IsUnique()
            .HasFilter("completed_at IS NULL AND expired_at IS NULL")
            .HasDatabaseName("ux_video_uploads_tenant_actor_fingerprint_pending");
        builder.HasIndex(upload => upload.VideoId)
            .IsUnique()
            .HasDatabaseName("ux_video_uploads_video_id");
    }
}
