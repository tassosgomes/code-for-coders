using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records", IdentitySchema.Name);
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(record => record.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(record => record.OperationId).HasColumnName("operation_id").HasMaxLength(128).IsRequired();
        builder.Property(record => record.KeyHash).HasColumnName("key_hash").HasMaxLength(64).IsRequired();
        builder.Property(record => record.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(record => record.StatusCode).HasColumnName("status_code").IsRequired();
        builder.Property(record => record.Code).HasColumnName("code").HasMaxLength(64);
        builder.Property(record => record.Title).HasColumnName("title").HasColumnType("text");
        builder.Property(record => record.StudentSessionId).HasColumnName("student_session_id").HasColumnType("uuid");
        builder.Property(record => record.StaffSessionId).HasColumnName("staff_session_id").HasColumnType("uuid");
        builder.Property(record => record.CreatedOn).HasColumnName("created_on").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(record => record.ExpiresOn).HasColumnName("expires_on").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(record => new { record.TenantId, record.OperationId, record.KeyHash })
            .HasDatabaseName("ux_idempotency_records_scope_key")
            .IsUnique();
        builder.HasIndex(record => record.ExpiresOn).HasDatabaseName("ix_idempotency_records_expires_on");
    }
}
