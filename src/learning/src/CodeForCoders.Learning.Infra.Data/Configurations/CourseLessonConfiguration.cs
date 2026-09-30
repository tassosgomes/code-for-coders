using CodeForCoders.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Configurations;

public sealed class CourseLessonConfiguration : IEntityTypeConfiguration<CourseLesson>
{
    public void Configure(EntityTypeBuilder<CourseLesson> builder)
    {
        builder.ToTable("course_lessons", "content");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.ModuleId).HasColumnName("module_id");
        builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(5000);
        builder.Property(item => item.Position).HasColumnName("position");
        builder.Property(item => item.VideoId).HasColumnName("video_id");
    }
}
