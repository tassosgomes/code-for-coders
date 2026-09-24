using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Audit.Infra.Data.Migrations;

/// <inheritdoc />
public partial class RebuildAdministrativeActRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_records",
            schema: "audit_access");

        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS audit_access.prevent_audit_record_mutation();
            """);

        migrationBuilder.CreateTable(
            name: "audit_records",
            schema: "audit_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                origem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                fato_id = table.Column<Guid>(type: "uuid", nullable: false),
                tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                autor_tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                autor_id = table.Column<Guid>(type: "uuid", nullable: true),
                alvo_tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                alvo_id = table.Column<Guid>(type: "uuid", nullable: true),
                complemento = table.Column<string>(type: "jsonb", nullable: true),
                motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                praticado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                conformidade = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                razoes = table.Column<string[]>(type: "text[]", nullable: false),
                impressao_digital = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_records", record => record.id);
            },
            comment: "Append-only audit evidence. UPDATE and DELETE are rejected by database trigger.");

        migrationBuilder.CreateIndex(
            name: "ix_audit_records_tenant_praticado_em",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "praticado_em" });

        migrationBuilder.CreateIndex(
            name: "ux_audit_records_origem_fato_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "origem", "fato_id" },
            unique: true);

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
            """);

        migrationBuilder.DropTable(
            name: "audit_records",
            schema: "audit_access");

        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS audit_access.prevent_audit_record_mutation();
            """);

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
                recorded_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_records", record => record.id);
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
}
