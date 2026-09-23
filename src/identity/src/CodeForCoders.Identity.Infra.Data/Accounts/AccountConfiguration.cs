using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", IdentitySchema.Name);
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(account => account.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(account => account.Type).HasColumnName("account_type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(account => account.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        builder.Property(account => account.Email).HasColumnName("email").HasColumnType("text").IsRequired();
        builder.Property(account => account.NormalizedEmail).HasColumnName("normalized_email").HasColumnType("text").IsRequired();
        builder.Property(account => account.IsConfirmed)
            .HasColumnName("is_confirmed")
            .IsRequired()
            .IsConcurrencyToken();
        builder.Property(account => account.DeactivatedOn).HasColumnName("deactivated_on").HasColumnType("timestamp with time zone");
        builder.HasAlternateKey(account => new { account.TenantId, account.Id })
            .HasName("ak_accounts_tenant_id_id");
        builder.HasIndex(account => new { account.TenantId, account.NormalizedEmail })
            .HasDatabaseName("ux_accounts_tenant_id_normalized_email")
            .IsUnique()
            .HasFilter("deactivated_on IS NULL");
    }
}
