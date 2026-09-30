using System.Text.Json;
using CodeForCoders.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Configurations;

public sealed class CourseVersionConfiguration : IEntityTypeConfiguration<CourseVersion>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<CourseVersion> builder)
    {
        builder.ToTable("course_versions", "content");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(version => version.TenantId).HasColumnName("tenant_id");
        builder.Property(version => version.CourseId).HasColumnName("course_id");
        builder.Property(version => version.VersionNumber).HasColumnName("version_number");
        builder.Property(version => version.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(version => version.Description).HasColumnName("description").HasMaxLength(5000);
        builder.Property(version => version.VersionNote).HasColumnName("version_note").HasMaxLength(1000);
        builder.Property(version => version.PublishedById).HasColumnName("published_by_id");
        builder.Property(version => version.PublishedByName).HasColumnName("published_by_name").HasMaxLength(200);
        builder.Property(version => version.PublishedAt).HasColumnName("published_at");
        builder.Property(version => version.Modules).HasColumnName("modules").HasColumnType("jsonb")
            .HasConversion(value => JsonSerializer.Serialize(value, JsonOptions),
                value => JsonSerializer.Deserialize<IReadOnlyList<PublishedModule>>(value, JsonOptions)!);
        builder.HasIndex(version => new { version.TenantId, version.CourseId, version.VersionNumber }).IsUnique();
        builder.HasOne<Course>().WithMany().HasForeignKey(version => version.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}
