using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments", CommerceSchemas.Entitlement);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.StudentId).HasColumnName("student_id");
        builder.Property(item => item.CourseId).HasColumnName("course_id");
        builder.Property(item => item.FirstGrantedAt).HasColumnName("first_granted_at");
        builder.HasIndex(item => new { item.TenantId, item.StudentId, item.CourseId }).IsUnique();
    }
}
