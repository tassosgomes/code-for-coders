using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Idempotency;

public sealed class OperationIdempotencyConfiguration : IEntityTypeConfiguration<OperationIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<OperationIdempotencyRecord> builder)
    {
        builder.ToTable("operation_idempotency");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(record => record.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(record => record.ActorAccountId)
            .HasColumnName("actor_account_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(record => record.Operation)
            .HasColumnName("operation")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(record => record.Key)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(record => record.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(record => record.ResponseStatusCode)
            .HasColumnName("response_status_code")
            .IsRequired();
        builder.Property(record => record.ResponseJson)
            .HasColumnName("response_json")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(record => record.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(record => new { record.TenantId, record.ActorAccountId, record.Operation, record.Key })
            .IsUnique()
            .HasDatabaseName("ux_operation_idempotency_scope_key");
        builder.HasIndex(record => record.ExpiresAt)
            .HasDatabaseName("ix_operation_idempotency_expires_at");
    }
}
