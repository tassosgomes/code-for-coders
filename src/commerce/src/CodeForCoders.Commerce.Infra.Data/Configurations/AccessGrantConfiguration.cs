using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class AccessGrantConfiguration : IEntityTypeConfiguration<AccessGrant>
{
    public void Configure(EntityTypeBuilder<AccessGrant> builder)
    {
        builder.ToTable("access_grants", CommerceSchemas.Entitlement);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.EnrollmentId).HasColumnName("enrollment_id");
        builder.Property(item => item.StudentId).HasColumnName("student_id");
        builder.Property(item => item.CourseId).HasColumnName("course_id");
        builder.Property(item => item.Origin).HasColumnName("origin").HasMaxLength(32);
        builder.Property(item => item.OriginRef).HasColumnName("origin_ref");
        builder.Property(item => item.PeriodType).HasColumnName("period_type").HasMaxLength(16);
        builder.Property(item => item.PeriodMonths).HasColumnName("period_months");
        builder.Property(item => item.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(item => item.GrantedAt).HasColumnName("granted_at");
        builder.Property(item => item.EndsOn).HasColumnName("ends_on");
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at");
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(item => item.GrantedBy).HasColumnName("granted_by");
        builder.Property(item => item.ExpiryEventId).HasColumnName("expiry_event_id");
        builder.Property(item => item.ExpiryPublishedAt).HasColumnName("expiry_published_at");
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(item => item.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TenantId, item.StudentId, item.CourseId });
        builder.HasIndex(item => new { item.TenantId, item.StudentId, item.GrantedAt }).IsDescending(false, false, true);
        builder.HasIndex(item => item.ExpiresAt).HasFilter("expiry_published_at IS NULL AND expires_at IS NOT NULL");
    }
}
