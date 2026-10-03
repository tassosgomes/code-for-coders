using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.PlaybackSessions;

public sealed class PlaybackSessionConfiguration : IEntityTypeConfiguration<PlaybackSession>
{
    public void Configure(EntityTypeBuilder<PlaybackSession> builder)
    {
        builder.ToTable("playback_sessions");
        builder.HasKey(session => session.SessionId);
        builder.Property(session => session.SessionId).HasColumnName("session_id").ValueGeneratedNever();
        builder.Property(session => session.TenantId).HasColumnName("tenant_id");
        builder.Property(session => session.StudentId).HasColumnName("student_id");
        builder.Property(session => session.LessonId).HasColumnName("lesson_id");
        builder.Property(session => session.CourseId).HasColumnName("course_id");
        builder.Property(session => session.VideoId).HasColumnName("video_id");
        builder.Property(session => session.CreatedAt).HasColumnName("created_at");
        builder.Property(session => session.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(session => session.ExpiresAt);
    }
}
