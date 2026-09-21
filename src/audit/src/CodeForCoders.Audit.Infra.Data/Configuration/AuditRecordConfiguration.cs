using CodeForCoders.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Audit.Infra.Data.Configuration;

public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_records", AuditSchema.Name, tableBuilder =>
        {
            tableBuilder.HasComment("Append-only audit evidence. UPDATE and DELETE are rejected by database trigger.");
        });
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(record => record.TenantId)
            .HasColumnName("tenant_id")
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(record => record.SourceService)
            .HasColumnName("source_service")
            .HasMaxLength(AuditRecord.SourceServiceMaxLength)
            .IsRequired();
        builder.Property(record => record.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(AuditRecord.EventTypeMaxLength)
            .IsRequired();
        builder.Property(record => record.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .HasMaxLength(AuditRecord.PayloadMaxLength)
            .IsRequired();
        builder.Property(record => record.OccurredOn)
            .HasColumnName("occurred_on")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(record => record.RecordedOn)
            .HasColumnName("recorded_on")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(record => new { record.TenantId, record.OccurredOn })
            .HasDatabaseName("ix_audit_records_tenant_occurred_on");
    }
}
