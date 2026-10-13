using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Transcriptions;

public sealed class TranscriptionEventConfiguration : IEntityTypeConfiguration<TranscriptionEvent>
{
    public void Configure(EntityTypeBuilder<TranscriptionEvent> builder)
    {
        builder.ToTable("video_transcription_events");
        builder.HasKey(notification => notification.EventId);
        builder.Property(notification => notification.EventId).HasColumnName("event_id").HasMaxLength(128).ValueGeneratedNever();
        builder.Property(notification => notification.TenantId).HasColumnName("tenant_id");
        builder.Property(notification => notification.ReceivedAt).HasColumnName("received_at");
        builder.HasIndex(notification => notification.ReceivedAt);
    }
}
