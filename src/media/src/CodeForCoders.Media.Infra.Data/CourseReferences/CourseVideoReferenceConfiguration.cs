using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.CourseReferences;

public sealed class CourseVideoReferenceConfiguration : IEntityTypeConfiguration<CourseVideoReference>
{
    public void Configure(EntityTypeBuilder<CourseVideoReference> builder)
    {
        builder.ToTable("course_video_references", "media_access");
        builder.HasKey(reference => new { reference.TenantId, reference.CourseId, reference.LessonId });
        builder.Property(reference => reference.TenantId).HasColumnName("tenant_id");
        builder.Property(reference => reference.CourseId).HasColumnName("course_id");
        builder.Property(reference => reference.LessonId).HasColumnName("lesson_id");
        builder.HasIndex(reference => new { reference.TenantId, reference.LessonId });
        builder.Property(reference => reference.VideoId).HasColumnName("video_id");
    }
}
