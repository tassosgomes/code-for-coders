using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class CatalogCourseViewConfiguration : IEntityTypeConfiguration<CatalogCourseView>
{
    public void Configure(EntityTypeBuilder<CatalogCourseView> builder)
    {
        builder.ToTable("course_views", CommerceSchemas.Catalog);
        builder.HasKey(course => new { course.TenantId, course.CourseId });
        builder.Property(course => course.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(course => course.CourseId).HasColumnName("course_id").ValueGeneratedNever();
        builder.Property(course => course.VersionNumber).HasColumnName("version_number");
        builder.Property(course => course.SourceFormat).HasColumnName("source_format").HasMaxLength(10);
        builder.Property(course => course.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(course => course.Description).HasColumnName("description").HasMaxLength(5000);
        builder.Property(course => course.Level).HasColumnName("level").HasMaxLength(20);
        builder.Property(course => course.PrerequisiteJson).HasColumnName("prerequisite").HasColumnType("jsonb");
        builder.Property(course => course.StructureJson).HasColumnName("structure").HasColumnType("jsonb");
        builder.Property(course => course.PublishedAt).HasColumnName("published_at");
        builder.Property(course => course.InShowcaseSince).HasColumnName("in_showcase_since");
        builder.Ignore(course => course.InShowcase);
        builder.HasIndex(course => new { course.TenantId, course.Title, course.CourseId });
    }
}
