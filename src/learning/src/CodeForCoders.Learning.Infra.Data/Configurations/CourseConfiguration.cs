using CodeForCoders.Learning.Domain.Entities;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Learning.Infra.Data.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses", "content");
        builder.HasKey(course => course.Id);
        builder.Property(course => course.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(course => course.TenantId).HasColumnName("tenant_id");
        builder.Property(course => course.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(course => course.TitleSearch).HasColumnName("title_search").HasMaxLength(200);
        builder.Property(course => course.Description).HasColumnName("description").HasMaxLength(5000);
        builder.Property(course => course.Level).HasColumnName("level").HasMaxLength(20);
        builder.Property(course => course.CurrentLevel).HasColumnName("current_level").HasMaxLength(20);
        builder.Property(course => course.PrerequisiteText).HasColumnName("prerequisite_text").HasMaxLength(1000);
        builder.Property(course => course.RecommendedCourseIds).HasColumnName("recommended_course_ids").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => JsonSerializer.Deserialize<IReadOnlyList<Guid>>(value, (JsonSerializerOptions?)null)!,
                new ValueComparer<IReadOnlyList<Guid>>((left, right) => left!.SequenceEqual(right!),
                    value => value.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)), value => value.ToArray()));
        builder.Property(course => course.DraftRevision).HasColumnName("draft_revision");
        builder.Property(course => course.CurrentVersion).HasColumnName("current_version");
        builder.Property(course => course.HasUnpublishedChanges).HasColumnName("has_unpublished_changes");
        builder.Property(course => course.PublishedFingerprint).HasColumnName("published_fingerprint").HasMaxLength(64);
        builder.Property(course => course.CreatedById).HasColumnName("created_by_id");
        builder.Property(course => course.CreatedByName).HasColumnName("created_by_name").HasMaxLength(200);
        builder.Property(course => course.CreatedAt).HasColumnName("created_at");
        builder.Property(course => course.LastEditedById).HasColumnName("last_edited_by_id");
        builder.Property(course => course.LastEditedByName).HasColumnName("last_edited_by_name").HasMaxLength(200);
        builder.Property(course => course.LastEditedAt).HasColumnName("last_edited_at");
        builder.HasMany(course => course.Modules).WithOne().HasForeignKey(module => module.CourseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(course => new { course.TenantId, course.LastEditedAt, course.Id });
    }
}
