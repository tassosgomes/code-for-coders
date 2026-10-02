using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class EntitlementCourseViewConfiguration : IEntityTypeConfiguration<EntitlementCourseView>
{
    public void Configure(EntityTypeBuilder<EntitlementCourseView> builder)
    {
        builder.ToTable("course_views", CommerceSchemas.Entitlement);
        builder.HasKey(course => new { course.TenantId, course.CourseId });
        builder.Property(course => course.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(course => course.CourseId).HasColumnName("course_id").ValueGeneratedNever();
        builder.Property(course => course.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(course => course.NormalizedTitle).HasColumnName("normalized_title").HasMaxLength(200);
        builder.Property(course => course.VersionNumber).HasColumnName("version_number");
        builder.HasIndex(course => new { course.TenantId, course.Title, course.CourseId });
    }
}
