using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> builder)
    {
        builder.ToTable("staff_invitations", IdentitySchema.Name);
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(invitation => invitation.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(invitation => invitation.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(invitation => invitation.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(invitation => invitation.OfferedRole)
            .HasColumnName("offered_role")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(invitation => invitation.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(invitation => invitation.InvitedOn)
            .HasColumnName("invited_on")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(invitation => invitation.ExpiresOn)
            .HasColumnName("expires_on")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(invitation => invitation.AcceptedOn)
            .HasColumnName("accepted_on")
            .HasColumnType("timestamp with time zone");
        builder.Property(invitation => invitation.SupersededOn)
            .HasColumnName("superseded_on")
            .HasColumnType("timestamp with time zone");
        builder.HasAlternateKey(invitation => new { invitation.TenantId, invitation.Id })
            .HasName("ak_staff_invitations_tenant_id_id");
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.NormalizedEmail })
            .HasDatabaseName("ux_staff_invitations_pending_email")
            .IsUnique()
            .HasFilter("accepted_on IS NULL AND superseded_on IS NULL");
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.InvitedOn })
            .HasDatabaseName("ix_staff_invitations_tenant_invited_on");
    }
}
