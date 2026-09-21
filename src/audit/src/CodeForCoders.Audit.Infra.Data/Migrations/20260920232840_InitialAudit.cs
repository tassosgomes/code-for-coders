using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Audit.Infra.Data.Migrations;

/// <inheritdoc />
public partial class InitialAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "audit_access");

        migrationBuilder.CreateTable(
            name: "audit_records",
            schema: "audit_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_service = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                payload = table.Column<string>(type: "jsonb", maxLength: 100000, nullable: false),
                occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                recorded_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_records", x => x.id);
            },
            comment: "Append-only audit evidence. UPDATE and DELETE are rejected by database trigger.");

        migrationBuilder.CreateIndex(
            name: "ix_audit_records_tenant_occurred_on",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "occurred_on" });

        migrationBuilder.Sql(
            """
                CREATE OR REPLACE FUNCTION audit_access.prevent_audit_record_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'Audit records are append-only; UPDATE and DELETE are forbidden.';
                END;
                $$;

                CREATE TRIGGER audit_records_append_only
                BEFORE UPDATE OR DELETE ON audit_access.audit_records
                FOR EACH ROW
                EXECUTE FUNCTION audit_access.prevent_audit_record_mutation();
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
                DROP TRIGGER IF EXISTS audit_records_append_only ON audit_access.audit_records;
                DROP FUNCTION IF EXISTS audit_access.prevent_audit_record_mutation();
                """);

        migrationBuilder.DropTable(
            name: "audit_records",
            schema: "audit_access");
    }
}
