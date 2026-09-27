using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.BffAdmin.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddAuditComplementConfirmation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "destination_exchange",
            schema: "bff_admin_access",
            table: "outbox_messages",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "payload_key_version",
            schema: "bff_admin_access",
            table: "outbox_messages",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "audit_complement_idempotency",
            schema: "bff_admin_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                request_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                confirmation_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_complement_idempotency", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_audit_complement_idempotency_expires_at",
            schema: "bff_admin_access",
            table: "audit_complement_idempotency",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ux_audit_complement_idempotency_scope_key",
            schema: "bff_admin_access",
            table: "audit_complement_idempotency",
            columns: new[] { "tenant_id", "actor_id", "operation_id", "idempotency_key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_complement_idempotency",
            schema: "bff_admin_access");

        migrationBuilder.DropColumn(
            name: "destination_exchange",
            schema: "bff_admin_access",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "payload_key_version",
            schema: "bff_admin_access",
            table: "outbox_messages");
    }
}
