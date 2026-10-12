using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Transcriptions;

public sealed class TranscriptionContentConfiguration : IEntityTypeConfiguration<TranscriptionContent>
{
    public void Configure(EntityTypeBuilder<TranscriptionContent> builder)
    {
        builder.ToTable("video_transcription_contents");
        builder.HasKey(content => content.TranscriptionId);
        builder.Property(content => content.TranscriptionId).HasColumnName("transcription_id").ValueGeneratedNever();
        builder.Property(content => content.TenantId).HasColumnName("tenant_id");
        builder.Property(content => content.Language).HasColumnName("language").HasMaxLength(16);
        builder.Property(content => content.DurationMs).HasColumnName("duration_ms");
        builder.Property(content => content.SegmentsJson).HasColumnName("segments").HasColumnType("jsonb");
        builder.HasOne<Transcription>().WithOne().HasForeignKey<TranscriptionContent>(content => content.TranscriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}
