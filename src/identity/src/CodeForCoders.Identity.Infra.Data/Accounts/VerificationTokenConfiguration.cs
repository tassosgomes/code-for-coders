using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class VerificationTokenConfiguration : IEntityTypeConfiguration<VerificationToken>
{
    public void Configure(EntityTypeBuilder<VerificationToken> builder)
    {
        builder.ToTable("verification_tokens", IdentitySchema.Name);
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(token => token.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(token => token.AccountId).HasColumnName("account_id").HasColumnType("uuid").IsRequired();
        builder.Property(token => token.Purpose).HasColumnName("purpose").HasMaxLength(64).IsRequired();
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.ExpiresOn).HasColumnName("expires_on").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(token => token.ConsumedOn).HasColumnName("consumed_on").HasColumnType("timestamp with time zone");
        builder.HasIndex(token => new { token.TenantId, token.TokenHash })
            .HasDatabaseName("ux_verification_tokens_tenant_id_token_hash")
            .IsUnique();
        builder.HasIndex(token => new { token.TenantId, token.AccountId, token.Purpose });
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(token => new { token.TenantId, token.AccountId })
            .HasPrincipalKey(account => new { account.TenantId, account.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
