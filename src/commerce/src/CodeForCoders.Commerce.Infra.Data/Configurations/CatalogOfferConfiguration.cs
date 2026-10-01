using System.Text.Json;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class CatalogOfferConfiguration : IEntityTypeConfiguration<CatalogOffer>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public void Configure(EntityTypeBuilder<CatalogOffer> builder)
    {
        builder.ToTable("offers", CommerceSchemas.Catalog);
        builder.HasKey(offer => offer.OfferId);
        builder.Property(offer => offer.OfferId).HasColumnName("offer_id").ValueGeneratedNever();
        builder.Property(offer => offer.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(offer => offer.CourseId).HasColumnName("course_id").ValueGeneratedNever();
        builder.Property(offer => offer.Name).HasColumnName("name").HasMaxLength(60);
        builder.Property(offer => offer.PriceCents).HasColumnName("price_cents");
        builder.Property(offer => offer.AccessPeriod).HasColumnName("access_period").HasColumnType("jsonb")
            .HasConversion(period => JsonSerializer.Serialize(period, JsonOptions), json => JsonSerializer.Deserialize<AccessPeriod>(json, JsonOptions)!);
        builder.Property(offer => offer.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property(offer => offer.OfferRevision).HasColumnName("offer_revision");
        builder.Property(offer => offer.CreatedAt).HasColumnName("created_at");
        builder.Property(offer => offer.UpdatedAt).HasColumnName("updated_at");
        builder.Property(offer => offer.PublishedAt).HasColumnName("published_at");
        builder.HasOne<CatalogCourseView>().WithMany(course => course.Offers)
            .HasForeignKey(offer => new { offer.TenantId, offer.CourseId }).OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
