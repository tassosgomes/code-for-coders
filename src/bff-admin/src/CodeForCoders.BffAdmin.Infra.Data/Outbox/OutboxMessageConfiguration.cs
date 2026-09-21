using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.BffAdmin.Infra.Data.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", BffAdminSchema.Name);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(message => message.TenantId).HasColumnName("tenant_id").ValueGeneratedNever().IsRequired();
        builder.Property(message => message.Type).HasColumnName("type").HasMaxLength(200).IsRequired();
        builder.Property(message => message.RoutingKey).HasColumnName("routing_key").HasMaxLength(200).IsRequired();
        builder.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.OccurredOn).HasColumnName("occurred_on").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(message => message.ProcessedOn).HasColumnName("processed_on").HasColumnType("timestamp with time zone");
        builder.Property(message => message.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(message => message.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.Property(message => message.TraceParent).HasColumnName("trace_parent").HasMaxLength(55);
        builder.HasIndex(message => message.Id).HasDatabaseName("ix_outbox_messages_pending").HasFilter("processed_on IS NULL");
        builder.HasIndex(message => message.TenantId).HasDatabaseName("ix_outbox_messages_tenant_id");
    }
}
