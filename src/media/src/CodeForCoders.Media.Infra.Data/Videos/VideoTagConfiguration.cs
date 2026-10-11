using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoTagConfiguration : IEntityTypeConfiguration<VideoTag>
{
    public void Configure(EntityTypeBuilder<VideoTag> builder)
    {
        builder.ToTable("tags");
        builder.HasKey(tag => tag.TagId);
        builder.Property(tag => tag.TagId).HasColumnName("tag_id").ValueGeneratedNever();
        builder.Property(tag => tag.TenantId).HasColumnName("tenant_id");
        builder.Property(tag => tag.DisplayName).HasColumnName("display_name").HasMaxLength(30).IsRequired();
        builder.Property(tag => tag.NormalizedName).HasColumnName("normalized_name").HasMaxLength(30).IsRequired();
        builder.Property(tag => tag.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(tag => new { tag.TenantId, tag.NormalizedName }).IsUnique();
    }
}
