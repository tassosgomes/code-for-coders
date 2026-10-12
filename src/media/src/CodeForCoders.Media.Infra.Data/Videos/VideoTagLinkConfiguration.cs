using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Videos;

public sealed class VideoTagLinkConfiguration : IEntityTypeConfiguration<VideoTagLink>
{
    public void Configure(EntityTypeBuilder<VideoTagLink> builder)
    {
        builder.ToTable("video_tags");
        builder.HasKey(link => new { link.VideoId, link.TagId });
        builder.Property(link => link.VideoId).HasColumnName("video_id");
        builder.Property(link => link.TagId).HasColumnName("tag_id");
        builder.Property(link => link.TenantId).HasColumnName("tenant_id");
        builder.HasOne<Video>().WithMany().HasForeignKey(link => link.VideoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<VideoTag>().WithMany().HasForeignKey(link => link.TagId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(link => new { link.TenantId, link.TagId });
    }
}
