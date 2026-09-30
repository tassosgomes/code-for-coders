using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.CourseReferences;

public sealed class CourseReferenceVersionConfiguration : IEntityTypeConfiguration<CourseReferenceVersion>
{
    public void Configure(EntityTypeBuilder<CourseReferenceVersion> builder)
    {
        builder.ToTable("course_reference_versions", "media_access");
        builder.HasKey(reference => new { reference.TenantId, reference.CourseId });
        builder.Property(reference => reference.TenantId).HasColumnName("tenant_id");
        builder.Property(reference => reference.CourseId).HasColumnName("course_id");
        builder.Property(reference => reference.VersionNumber).HasColumnName("version_number");
    }
}
