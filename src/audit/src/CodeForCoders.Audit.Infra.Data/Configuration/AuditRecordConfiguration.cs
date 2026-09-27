using CodeForCoders.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Audit.Infra.Data.Configuration;

public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public const string UniqueOriginFactIdIndexName = "ux_audit_records_origem_fato_id";

    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_records", AuditSchema.Name, tableBuilder =>
        {
            tableBuilder.HasComment("Append-only audit evidence. UPDATE and DELETE are rejected by database trigger.");
            tableBuilder.HasCheckConstraint(
                "ck_audit_records_record_type",
                "\"record_type\" IN ('original', 'complement')");
            tableBuilder.HasCheckConstraint(
                "ck_audit_records_complement_shape",
                "(\"record_type\" = 'original' AND \"original_record_id\" IS NULL AND \"confirmation_id\" IS NULL AND \"confirmed_at\" IS NULL AND \"explanation\" IS NULL) OR (\"record_type\" = 'complement' AND \"original_record_id\" IS NOT NULL AND \"confirmation_id\" IS NOT NULL AND \"confirmed_at\" IS NOT NULL AND \"explanation\" IS NOT NULL)");
        });
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(record => record.TenantId)
            .HasColumnName("tenant_id")
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(record => record.RecordType)
            .HasColumnName("record_type")
            .HasMaxLength(16)
            .HasDefaultValue(AuditRecord.OriginalRecordType)
            .IsRequired();
        builder.Property(record => record.Origin)
            .HasColumnName("origem")
            .HasMaxLength(AuditRecord.OriginMaxLength)
            .IsRequired();
        builder.Property(record => record.FactId)
            .HasColumnName("fato_id")
            .IsRequired();
        builder.Property(record => record.OriginalRecordId)
            .HasColumnName("original_record_id")
            .IsRequired(false);
        builder.Property(record => record.ConfirmationId)
            .HasColumnName("confirmation_id")
            .IsRequired(false);
        builder.Property(record => record.ConfirmedAt)
            .HasColumnName("confirmed_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);
        builder.Property(record => record.Explanation)
            .HasColumnName("explanation")
            .HasColumnType("text")
            .IsRequired(false);
        builder.Property(record => record.Type)
            .HasColumnName("tipo")
            .HasColumnType("text")
            .IsRequired(false);
        builder.Property(record => record.AuthorType)
            .HasColumnName("autor_tipo")
            .HasColumnType("text")
            .IsRequired(false);
        builder.Property(record => record.AuthorId)
            .HasColumnName("autor_id")
            .IsRequired(false);
        builder.Property(record => record.TargetType)
            .HasColumnName("alvo_tipo")
            .HasColumnType("text")
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
            .HasColumnType("text")
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

        builder.HasAlternateKey(record => new { record.TenantId, record.Id })
            .HasName("ak_audit_records_tenant_id_id");
        builder.HasOne<AuditRecord>()
            .WithMany()
            .HasForeignKey(record => new { record.TenantId, record.OriginalRecordId })
            .HasPrincipalKey(record => new { record.TenantId, record.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_records_original_record");

        builder.HasIndex(record => new { record.Origin, record.FactId })
            .IsUnique()
            .HasFilter("\"record_type\" = 'original'")
            .HasDatabaseName(UniqueOriginFactIdIndexName);
        builder.HasIndex(record => new { record.TenantId, record.ConfirmationId })
            .IsUnique()
            .HasDatabaseName("ux_audit_records_tenant_confirmation_id")
            .HasFilter("\"confirmation_id\" IS NOT NULL");
        builder.HasIndex(record => new { record.TenantId, record.PracticedOn })
            .HasDatabaseName("ix_audit_records_tenant_praticado_em");
        builder.HasIndex(record => new { record.TenantId, record.PracticedOn, record.Id })
            .HasDatabaseName("ix_audit_records_tenant_praticado_em_id");
        builder.HasIndex(record => new { record.TenantId, record.Type, record.PracticedOn, record.Id })
            .HasDatabaseName("ix_audit_records_tenant_tipo_praticado_em_id");
        builder.HasIndex(record => new { record.TenantId, record.AuthorId, record.PracticedOn, record.Id })
            .HasDatabaseName("ix_audit_records_tenant_autor_praticado_em_id");
        builder.HasIndex(record => new { record.TenantId, record.TargetId, record.PracticedOn, record.Id })
            .HasDatabaseName("ix_audit_records_tenant_alvo_praticado_em_id");
        builder.HasIndex(record => new { record.TenantId, record.Conformity, record.PracticedOn, record.Id })
            .HasDatabaseName("ix_audit_records_tenant_conformidade_praticado_em_id");
    }
}
