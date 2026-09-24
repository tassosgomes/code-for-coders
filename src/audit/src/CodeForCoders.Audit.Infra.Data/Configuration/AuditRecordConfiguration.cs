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
        builder.Property(record => record.Origin)
            .HasColumnName("origem")
            .HasMaxLength(AuditRecord.OriginMaxLength)
            .IsRequired();
        builder.Property(record => record.FactId)
            .HasColumnName("fato_id")
            .IsRequired();
        builder.Property(record => record.Type)
            .HasColumnName("tipo")
            .HasMaxLength(AuditRecord.TypeMaxLength)
            .IsRequired();
        builder.Property(record => record.AuthorType)
            .HasColumnName("autor_tipo")
            .HasMaxLength(AuditRecord.ReferenceTypeMaxLength)
            .IsRequired(false);
        builder.Property(record => record.AuthorId)
            .HasColumnName("autor_id")
            .IsRequired(false);
        builder.Property(record => record.TargetType)
            .HasColumnName("alvo_tipo")
            .HasMaxLength(AuditRecord.ReferenceTypeMaxLength)
            .IsRequired(false);
        builder.Property(record => record.TargetId)
            .HasColumnName("alvo_id")
            .IsRequired(false);
        builder.Property(record => record.Complement)
            .HasColumnName("complemento")
            .HasColumnType("jsonb")
            .IsRequired(false);
        builder.Property(record => record.Reason)
            .HasColumnName("motivo")
            .HasMaxLength(AuditRecord.ReasonMaxLength)
            .IsRequired(false);
        builder.Property(record => record.PracticedOn)
            .HasColumnName("praticado_em")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);
        builder.Property(record => record.ReceivedOn)
            .HasColumnName("recebido_em")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(record => record.Conformity)
            .HasColumnName("conformidade")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(record => record.Reasons)
            .HasColumnName("razoes")
            .HasColumnType("text[]")
            .IsRequired();
        builder.Property(record => record.Fingerprint)
            .HasColumnName("impressao_digital")
            .HasMaxLength(AuditRecord.FingerprintLength)
            .IsRequired();

        builder.HasIndex(record => new { record.Origin, record.FactId })
            .IsUnique()
            .HasDatabaseName("ux_audit_records_origem_fato_id");
        builder.HasIndex(record => new { record.TenantId, record.PracticedOn })
            .HasDatabaseName("ix_audit_records_tenant_praticado_em");
    }
}
