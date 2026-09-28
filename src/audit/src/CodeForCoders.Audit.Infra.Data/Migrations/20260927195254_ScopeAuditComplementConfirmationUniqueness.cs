using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Audit.Infra.Data.Migrations;

/// <inheritdoc />
public partial class ScopeAuditComplementConfirmationUniqueness : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_audit_records_origem_fato_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.CreateIndex(
            name: "ux_audit_records_origem_fato_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "origem", "fato_id" },
            unique: true,
            filter: "\"record_type\" = 'original'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_audit_records_origem_fato_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.CreateIndex(
            name: "ux_audit_records_origem_fato_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "origem", "fato_id" },
            unique: true);
    }
}
