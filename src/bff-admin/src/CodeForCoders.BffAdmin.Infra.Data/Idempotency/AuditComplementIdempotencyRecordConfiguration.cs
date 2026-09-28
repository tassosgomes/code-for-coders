using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.BffAdmin.Infra.Data.Idempotency;

public sealed class AuditComplementIdempotencyRecordConfiguration : IEntityTypeConfiguration<AuditComplementIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<AuditComplementIdempotencyRecord> builder)
    {
        builder.ToTable("audit_complement_idempotency", BffAdminSchema.Name);
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(record => record.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(record => record.ActorId).HasColumnName("actor_id").HasColumnType("uuid").IsRequired();
        builder.Property(record => record.OperationId).HasColumnName("operation_id").HasMaxLength(128).IsRequired();
        builder.Property(record => record.IdempotencyKey).HasColumnName("idempotency_key").HasColumnType("uuid").IsRequired();
        builder.Property(record => record.RequestHash).HasColumnName("request_hash").HasColumnType("bytea").IsRequired();
        builder.Property(record => record.ConfirmationId).HasColumnName("confirmation_id").HasColumnType("uuid").IsRequired();
        builder.Property(record => record.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(record => record.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(record => new { record.TenantId, record.ActorId, record.OperationId, record.IdempotencyKey })
            .HasDatabaseName("ux_audit_complement_idempotency_scope_key")
            .IsUnique();
        builder.HasIndex(record => record.ExpiresAt).HasDatabaseName("ix_audit_complement_idempotency_expires_at");
    }
}
