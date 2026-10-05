using CodeForCoders.Learning.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Progress;

public sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> builder)
    {
        builder.ToTable("lesson_progress", LearningSchemas.Progress);
        builder.HasKey(item => new { item.TenantId, item.StudentId, item.LessonId });
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.StudentId).HasColumnName("student_id").ValueGeneratedNever();
        builder.Property(item => item.LessonId).HasColumnName("lesson_id").ValueGeneratedNever();
        builder.Property(item => item.CourseId).HasColumnName("course_id").ValueGeneratedNever();
        builder.Property(item => item.LastPositionSeconds).HasColumnName("last_position_seconds");
        builder.Property(item => item.OccurredAt).HasColumnName("occurred_at");
        builder.Property(item => item.Sequence).HasColumnName("sequence");
        builder.Property(item => item.Reason).HasColumnName("reason");
        builder.Property(item => item.MaxPositionSeconds).HasColumnName("max_position_seconds");
        builder.Property(item => item.CompletedAt).HasColumnName("completed_at");
        builder.Property(item => item.LastActivityAt).HasColumnName("last_activity_at");
        builder.HasIndex(item => new { item.TenantId, item.CourseId, item.StudentId });
    }
}
