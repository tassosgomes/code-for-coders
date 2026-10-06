using CodeForCoders.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CodeForCoders.Billing.Infra.Data.Payments;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property<string>("Namespace").HasColumnName("namespace").HasMaxLength(100);
        b.Property(x => x.TenantId).HasColumnName("tenant_id"); b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.StudentId).HasColumnName("student_id"); b.Property(x => x.AmountCents).HasColumnName("amount_cents");
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3); b.Property(x => x.Description).HasColumnName("description").HasMaxLength(263);
        b.Property(x => x.SessionReference).HasColumnName("session_reference").HasMaxLength(255);
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at"); b.Property(x => x.GatewayReference).HasColumnName("gateway_reference").HasMaxLength(255);
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(30); b.Property(x => x.Method).HasColumnName("method").HasMaxLength(30);
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(30);
        b.HasIndex("Namespace", nameof(Payment.TenantId), nameof(Payment.OrderId)).IsUnique();
    }
}
