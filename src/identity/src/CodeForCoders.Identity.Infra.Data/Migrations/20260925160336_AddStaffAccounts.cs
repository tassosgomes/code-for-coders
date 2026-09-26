using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Identity.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddStaffAccounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "staff_role_assignments",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                assigned_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_staff_role_assignments", x => x.id);
                table.UniqueConstraint("ak_staff_role_assignments_tenant_id_id", x => new { x.tenant_id, x.id });
                table.ForeignKey(
                    name: "FK_staff_role_assignments_accounts_tenant_id_account_id",
                    columns: x => new { x.tenant_id, x.account_id },
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "staff_sessions",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_staff_sessions", x => x.id);
                table.UniqueConstraint("ak_staff_sessions_tenant_id_id", x => new { x.tenant_id, x.id });
                table.ForeignKey(
                    name: "FK_staff_sessions_accounts_tenant_id_account_id",
                    columns: x => new { x.tenant_id, x.account_id },
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_staff_role_assignments_tenant_account_role",
            schema: "identity_access",
            table: "staff_role_assignments",
            columns: new[] { "tenant_id", "account_id", "role" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_staff_sessions_expires_on",
            schema: "identity_access",
            table: "staff_sessions",
            column: "expires_on");

        migrationBuilder.CreateIndex(
            name: "ix_staff_sessions_tenant_id_account_id",
            schema: "identity_access",
            table: "staff_sessions",
            columns: new[] { "tenant_id", "account_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "staff_role_assignments",
            schema: "identity_access");

        migrationBuilder.DropTable(
            name: "staff_sessions",
            schema: "identity_access");
    }
}
