using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Audit.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddAuditRecordComplements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "confirmation_id",
            schema: "audit_access",
            table: "audit_records",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "confirmed_at",
            schema: "audit_access",
            table: "audit_records",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "explanation",
            schema: "audit_access",
            table: "audit_records",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "original_record_id",
            schema: "audit_access",
            table: "audit_records",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "record_type",
            schema: "audit_access",
            table: "audit_records",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "original");

        migrationBuilder.AddUniqueConstraint(
            name: "ak_audit_records_tenant_id_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "id" });

        migrationBuilder.CreateIndex(
            name: "IX_audit_records_tenant_id_original_record_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "original_record_id" });

        migrationBuilder.CreateIndex(
            name: "ux_audit_records_tenant_confirmation_id",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "confirmation_id" },
            unique: true,
            filter: "\"confirmation_id\" IS NOT NULL");

        migrationBuilder.AddCheckConstraint(
            name: "ck_audit_records_complement_shape",
            schema: "audit_access",
            table: "audit_records",
            sql: "(\"record_type\" = 'original' AND \"original_record_id\" IS NULL AND \"confirmation_id\" IS NULL AND \"confirmed_at\" IS NULL AND \"explanation\" IS NULL) OR (\"record_type\" = 'complement' AND \"original_record_id\" IS NOT NULL AND \"confirmation_id\" IS NOT NULL AND \"confirmed_at\" IS NOT NULL AND \"explanation\" IS NOT NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_audit_records_record_type",
            schema: "audit_access",
            table: "audit_records",
            sql: "\"record_type\" IN ('original', 'complement')");

        migrationBuilder.AddForeignKey(
            name: "fk_audit_records_original_record",
            schema: "audit_access",
            table: "audit_records",
            columns: new[] { "tenant_id", "original_record_id" },
            principalSchema: "audit_access",
            principalTable: "audit_records",
            principalColumns: new[] { "tenant_id", "id" },
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_audit_records_original_record",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropUniqueConstraint(
            name: "ak_audit_records_tenant_id_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropIndex(
            name: "IX_audit_records_tenant_id_original_record_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropIndex(
            name: "ux_audit_records_tenant_confirmation_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropCheckConstraint(
            name: "ck_audit_records_complement_shape",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropCheckConstraint(
            name: "ck_audit_records_record_type",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropColumn(
            name: "confirmation_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropColumn(
            name: "confirmed_at",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropColumn(
            name: "explanation",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropColumn(
            name: "original_record_id",
            schema: "audit_access",
            table: "audit_records");

        migrationBuilder.DropColumn(
            name: "record_type",
            schema: "audit_access",
            table: "audit_records");
    }
}
