using CodeForCoders.Notification.Domain.DeliveryRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Notification.Infra.Data.Configuration;

public sealed class DeliveryOutcomeCounterConfiguration : IEntityTypeConfiguration<DeliveryOutcomeCounter>
{
    public void Configure(EntityTypeBuilder<DeliveryOutcomeCounter> builder)
    {
        builder.ToTable("delivery_outcome_counters", NotificationSchema.Name);
        builder.HasKey(counter => new
        {
            counter.Namespace,
            counter.TenantId,
            counter.Purpose,
            counter.Status,
            counter.OutcomeDay,
        }).HasName("pk_delivery_outcome_counters");

        builder.Property(counter => counter.Namespace)
            .HasColumnName("namespace")
            .HasMaxLength(DeliveryRecord.NamespaceMaxLength)
            .IsRequired();
        builder.Property(counter => counter.TenantId)
            .HasColumnName("tenant_id")
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(counter => counter.Purpose)
            .HasColumnName("purpose")
            .HasMaxLength(DeliveryRecord.PurposeMaxLength)
            .IsRequired();
        builder.Property(counter => counter.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<DeliveryStatus>(value, ignoreCase: true))
            .IsRequired();
        builder.Property(counter => counter.OutcomeDay)
            .HasColumnName("outcome_day")
            .HasColumnType("date")
            .IsRequired();
        builder.Property(counter => counter.Count)
            .HasColumnName("count")
            .IsRequired();
    }
}
