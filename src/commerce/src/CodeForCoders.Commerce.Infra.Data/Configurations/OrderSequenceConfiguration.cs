using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CodeForCoders.Commerce.Infra.Data.Configurations;

public sealed class OrderSequenceConfiguration : IEntityTypeConfiguration<OrderSequence>
{
    public void Configure(EntityTypeBuilder<OrderSequence> builder)
    {
        builder.ToTable("order_sequences", CommerceSchemas.Sales);
        builder.HasKey(item => item.TenantId);
        builder.Property(item => item.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
        builder.Property(item => item.Value).HasColumnName("value");

    }
}
