using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.ToTable("credentials", IdentitySchema.Name);
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(credential => credential.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(credential => credential.AccountId).HasColumnName("account_id").HasColumnType("uuid").IsRequired();
        builder.Property(credential => credential.PasswordHash).HasColumnName("password_hash").HasColumnType("text").IsRequired();
        builder.Property(credential => credential.CreatedOn).HasColumnName("created_on").HasColumnType("timestamp with time zone").IsRequired();
        // Optimistic concurrency guard (RACE #8): a password reset/change bumps the row's xmin. A
        // login that authenticated against the previous hash touches this row under the xmin it read,
        // so its commit fails if the credential changed meanwhile and the session never materializes.
        // The uint row-version shadow property maps to PostgreSQL's xmin system column (no DDL).
        builder.Property<uint>("xmin").IsRowVersion();
        builder.HasIndex(credential => new { credential.TenantId, credential.AccountId })
            .HasDatabaseName("ux_credentials_tenant_id_account_id")
            .IsUnique();
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(credential => new { credential.TenantId, credential.AccountId })
            .HasPrincipalKey(account => new { account.TenantId, account.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
