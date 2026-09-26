using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class StaffSessionConfiguration : IEntityTypeConfiguration<StaffSession>
{
    public void Configure(EntityTypeBuilder<StaffSession> builder)
    {
        builder.ToTable("staff_sessions", IdentitySchema.Name);
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(session => session.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(session => session.AccountId)
            .HasColumnName("account_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(session => session.CreatedOn)
            .HasColumnName("created_on")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(session => session.ExpiresOn)
            .HasColumnName("expires_on")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(session => session.RevokedOn)
            .HasColumnName("revoked_on")
            .HasColumnType("timestamp with time zone")
            .IsConcurrencyToken();
        builder.HasAlternateKey(session => new { session.TenantId, session.Id })
            .HasName("ak_staff_sessions_tenant_id_id");
        builder.HasIndex(session => new { session.TenantId, session.AccountId })
            .HasDatabaseName("ix_staff_sessions_tenant_id_account_id");
        builder.HasIndex(session => session.ExpiresOn)
            .HasDatabaseName("ix_staff_sessions_expires_on");
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(session => new { session.TenantId, session.AccountId })
            .HasPrincipalKey(account => new { account.TenantId, account.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
