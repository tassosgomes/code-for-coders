using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", CommerceSchemas.Sales);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.StudentId).HasColumnName("student_id");
        builder.Property(item => item.Number).HasColumnName("number").HasMaxLength(30);
        builder.Property(item => item.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(item => item.CourseId).HasColumnName("course_id");
        builder.Property(item => item.CourseTitle).HasColumnName("course_title").HasMaxLength(500);
        builder.Property(item => item.OfferId).HasColumnName("offer_id");
        builder.Property(item => item.OfferName).HasColumnName("offer_name").HasMaxLength(60);
        builder.Property(item => item.PriceCents).HasColumnName("price_cents");
        builder.Property(item => item.Currency).HasColumnName("currency").HasMaxLength(3);
        builder.Property(item => item.PeriodType).HasColumnName("period_type").HasMaxLength(20);
        builder.Property(item => item.PeriodMonths).HasColumnName("period_months");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.PaymentPageExpiresAt).HasColumnName("payment_page_expires_at");
        builder.Property(item => item.PaidAt).HasColumnName("paid_at");
        builder.Property(item => item.PaymentMethod).HasColumnName("payment_method").HasMaxLength(255);
        builder.Property(item => item.PaidAmountCents).HasColumnName("paid_amount_cents");
        builder.Property(item => item.GatewayReference).HasColumnName("gateway_reference").HasMaxLength(255);
        builder.Property(item => item.GrantId).HasColumnName("grant_id");
        builder.Property(item => item.AccessGrantedAt).HasColumnName("access_granted_at");
        builder.HasIndex(item => new { item.TenantId, item.StudentId, item.OfferId }).IsUnique().HasFilter("status = 'awaiting-payment'").HasDatabaseName("ux_orders_pending");
        builder.HasIndex(item => new { item.TenantId, item.Number }).IsUnique();
    }
}
