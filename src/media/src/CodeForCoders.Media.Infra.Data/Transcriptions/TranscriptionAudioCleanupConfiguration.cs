using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Transcriptions;

public sealed class TranscriptionAudioCleanupConfiguration : IEntityTypeConfiguration<TranscriptionAudioCleanup>
{
    public void Configure(EntityTypeBuilder<TranscriptionAudioCleanup> builder)
    {
        builder.ToTable("transcription_audio_cleanups");
        builder.HasKey(row => row.TranscriptionId);
        builder.Property(row => row.TranscriptionId).HasColumnName("transcription_id").ValueGeneratedNever();
        builder.Property(row => row.TenantId).HasColumnName("tenant_id");
        builder.Property(row => row.VideoId).HasColumnName("video_id");
        builder.Property(row => row.RequestedAt).HasColumnName("requested_at");
        builder.HasIndex(row => new { row.RequestedAt, row.TranscriptionId })
            .HasDatabaseName("ix_transcription_audio_cleanups_requested_at_transcription_id");
    }
}
