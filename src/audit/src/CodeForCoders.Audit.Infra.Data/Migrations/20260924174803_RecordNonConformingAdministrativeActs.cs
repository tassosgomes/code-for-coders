using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Audit.Infra.Data.Migrations;

/// <inheritdoc />
public partial class RecordNonConformingAdministrativeActs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "tipo",
            schema: "audit_access",
            table: "audit_records",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200);

        migrationBuilder.AlterColumn<string>(
            name: "motivo",
            schema: "audit_access",
            table: "audit_records",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(1000)",
            oldMaxLength: 1000,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "autor_tipo",
            schema: "audit_access",
            table: "audit_records",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "alvo_tipo",
            schema: "audit_access",
            table: "audit_records",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "tipo",
            schema: "audit_access",
            table: "audit_records",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "motivo",
            schema: "audit_access",
            table: "audit_records",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "autor_tipo",
            schema: "audit_access",
            table: "audit_records",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "alvo_tipo",
            schema: "audit_access",
            table: "audit_records",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }
}
