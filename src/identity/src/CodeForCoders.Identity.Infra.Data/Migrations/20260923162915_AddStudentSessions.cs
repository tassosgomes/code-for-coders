using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Identity.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddStudentSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "student_session_id",
            schema: "identity_access",
            table: "idempotency_records",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "student_sessions",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_activity_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_student_sessions", x => x.id);
                table.UniqueConstraint("ak_student_sessions_tenant_id_id", x => new { x.tenant_id, x.id });
                table.ForeignKey(
                    name: "FK_student_sessions_accounts_tenant_id_account_id",
                    columns: x => new { x.tenant_id, x.account_id },
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_student_sessions_expires_on",
            schema: "identity_access",
            table: "student_sessions",
            column: "expires_on");

        migrationBuilder.CreateIndex(
            name: "ix_student_sessions_tenant_id_account_id",
            schema: "identity_access",
            table: "student_sessions",
            columns: new[] { "tenant_id", "account_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "student_sessions",
            schema: "identity_access");

        migrationBuilder.DropColumn(
            name: "student_session_id",
            schema: "identity_access",
            table: "idempotency_records");
    }
}
