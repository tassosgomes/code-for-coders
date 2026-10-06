using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CodeForCoders.Billing.Infra.Data.Payments;

public sealed class GatewayInboxConfiguration : IEntityTypeConfiguration<GatewayInboxEntry>
{
    public void Configure(EntityTypeBuilder<GatewayInboxEntry> b)
    {
        b.ToTable("gateway_inbox"); b.HasKey(x => new { x.Namespace, x.Id });
        b.Property(x => x.Id).HasColumnName("id").HasMaxLength(255);
        b.Property(x => x.Namespace).HasColumnName("namespace").HasMaxLength(255);
        b.Property(x => x.Type).HasColumnName("type").HasMaxLength(255);
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.Property(x => x.ObjectReference).HasColumnName("object_reference").HasMaxLength(255);
        b.Property(x => x.SessionReference).HasColumnName("session_reference").HasMaxLength(255);
        b.Property(x => x.PaymentReference).HasColumnName("payment_reference").HasMaxLength(255);
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(255);
        b.Property(x => x.Method).HasColumnName("method").HasMaxLength(255);
        b.Property(x => x.AmountCents).HasColumnName("amount_cents");
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(255);
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(255);
        b.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        b.HasIndex(x => x.ProcessedAt).HasFilter("processed_at IS NULL");
    }
}
