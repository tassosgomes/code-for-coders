using CodeForCoders.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Configurations;

public sealed class CourseModuleConfiguration : IEntityTypeConfiguration<CourseModule>
{
    public void Configure(EntityTypeBuilder<CourseModule> builder)
    {
        builder.ToTable("course_modules", "content");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.CourseId).HasColumnName("course_id");
        builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(item => item.Position).HasColumnName("position");
        builder.HasMany(item => item.Lessons).WithOne().HasForeignKey(item => item.ModuleId).OnDelete(DeleteBehavior.Cascade);
    }
}
