using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class StaffRoleAssignmentConfiguration : IEntityTypeConfiguration<StaffRoleAssignment>
{
    public void Configure(EntityTypeBuilder<StaffRoleAssignment> builder)
    {
        builder.ToTable("staff_role_assignments", IdentitySchema.Name);
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(assignment => assignment.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(assignment => assignment.AccountId)
            .HasColumnName("account_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(assignment => assignment.Role)
            .HasColumnName("role")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(assignment => assignment.AssignedOn)
            .HasColumnName("assigned_on")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.HasAlternateKey(assignment => new { assignment.TenantId, assignment.Id })
            .HasName("ak_staff_role_assignments_tenant_id_id");
        builder.HasIndex(assignment => new { assignment.TenantId, assignment.AccountId, assignment.Role })
            .IsUnique()
            .HasDatabaseName("ux_staff_role_assignments_tenant_account_role");
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.TenantId, assignment.AccountId })
            .HasPrincipalKey(account => new { account.TenantId, account.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
