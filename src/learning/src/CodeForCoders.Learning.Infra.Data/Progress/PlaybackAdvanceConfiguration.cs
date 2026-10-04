using CodeForCoders.Learning.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Progress;

public sealed class PlaybackAdvanceConfiguration : IEntityTypeConfiguration<PlaybackAdvance>
{
    public void Configure(EntityTypeBuilder<PlaybackAdvance> builder)
    {
        builder.ToTable("playback_advances", LearningSchemas.Progress);
        builder.HasKey(item => item.EventId);
        builder.Property(item => item.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.SessionId).HasColumnName("session_id").ValueGeneratedNever();
        builder.Property(item => item.StudentId).HasColumnName("student_id").ValueGeneratedNever();
        builder.Property(item => item.CourseId).HasColumnName("course_id").ValueGeneratedNever();
        builder.Property(item => item.LessonId).HasColumnName("lesson_id").ValueGeneratedNever();
        builder.Property(item => item.Sequence).HasColumnName("sequence");
        builder.Property(item => item.PositionSeconds).HasColumnName("position_seconds");
        builder.Property(item => item.Reason).HasColumnName("reason");
        builder.Property(item => item.OccurredAt).HasColumnName("occurred_at");
        builder.Property(item => item.ReceivedAt).HasColumnName("received_at");
        builder.HasIndex(item => new { item.TenantId, item.StudentId, item.CourseId });
        builder.HasIndex(item => new { item.TenantId, item.OccurredAt });
    }
}
