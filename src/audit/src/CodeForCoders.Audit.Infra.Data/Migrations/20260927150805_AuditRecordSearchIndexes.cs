using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Audit.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AuditRecordSearchIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_audit_records_tenant_alvo_praticado_em_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "alvo_id", "praticado_em", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_records_tenant_autor_praticado_em_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "autor_id", "praticado_em", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_records_tenant_conformidade_praticado_em_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "conformidade", "praticado_em", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_records_tenant_praticado_em_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "praticado_em", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_records_tenant_tipo_praticado_em_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "tipo", "praticado_em", "id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_audit_records_tenant_alvo_praticado_em_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropIndex(
            name: "ix_audit_records_tenant_autor_praticado_em_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropIndex(
            name: "ix_audit_records_tenant_conformidade_praticado_em_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropIndex(
            name: "ix_audit_records_tenant_praticado_em_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropIndex(
            name: "ix_audit_records_tenant_tipo_praticado_em_id",
            schema: "audit_access",
            table: "audit_records");
    }
}
