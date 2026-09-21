using CodeForCoders.Notification.Domain.DeliveryRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Notification.Infra.Data.Configuration;

public sealed class DeliveryRecordConfiguration : IEntityTypeConfiguration<DeliveryRecord>
{
    public void Configure(EntityTypeBuilder<DeliveryRecord> builder)
    {
        builder.ToTable("delivery_records", NotificationSchema.Name);
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(record => record.TenantId)
            .HasColumnName("tenant_id")
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(record => record.RequestId)
            .HasColumnName("request_id")
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(record => record.Recipient)
            .HasColumnName("recipient")
            .HasMaxLength(DeliveryRecord.RecipientMaxLength);
        builder.Property(record => record.RecipientName)
            .HasColumnName("recipient_name")
            .HasMaxLength(DeliveryRecord.RecipientNameMaxLength);
        builder.Property(record => record.Link)
            .HasColumnName("link")
            .HasMaxLength(DeliveryRecord.LinkMaxLength);
        builder.Property(record => record.Purpose)
            .HasColumnName("purpose")
            .HasMaxLength(DeliveryRecord.PurposeMaxLength);
        builder.Property(record => record.Model)
            .HasColumnName("model")
            .HasMaxLength(DeliveryRecord.ModelMaxLength);
        builder.Property(record => record.Reason)
            .HasColumnName("reason")
            .HasMaxLength(DeliveryRecord.ReasonMaxLength);
        builder.Property(record => record.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(DeliveryRecord.CorrelationIdMaxLength);
        builder.Property(record => record.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<DeliveryStatus>(value, ignoreCase: true))
            .IsRequired();
        builder.Property(record => record.RequestedOn)
            .HasColumnName("requested_on")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(record => record.AcceptedOn)
            .HasColumnName("accepted_on")
            .HasColumnType("timestamp with time zone");
        builder.Property(record => record.RefusedOn)
            .HasColumnName("refused_on")
            .HasColumnType("timestamp with time zone");
        builder.Property(record => record.DeliveredOn)
            .HasColumnName("delivered_on")
            .HasColumnType("timestamp with time zone");
        builder.Property(record => record.FailedOn)
            .HasColumnName("failed_on")
            .HasColumnType("timestamp with time zone");
        builder.Property(record => record.ProviderAttemptCount)
            .HasColumnName("provider_attempt_count")
            .IsRequired();
        builder.Property(record => record.LastProviderAttemptOn)
            .HasColumnName("last_provider_attempt_on")
            .HasColumnType("timestamp with time zone");
        builder.Property(record => record.NextAttemptOn)
            .HasColumnName("next_attempt_on")
            .HasColumnType("timestamp with time zone");
        builder.Property(record => record.ExhaustedAttempts)
            .HasColumnName("exhausted_attempts")
            .IsRequired();

        builder.HasIndex(record => new { record.TenantId, record.RequestId })
            .IsUnique()
            .HasDatabaseName("ux_delivery_records_tenant_id_request_id");
        builder.HasIndex(record => new { record.Status, record.AcceptedOn })
            .HasDatabaseName("ix_delivery_records_status_accepted_on");
    }
}
