using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Identity.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddStudentRegistration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "exchange",
            schema: "identity_access",
            table: "outbox_messages",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "identity.events");

        migrationBuilder.AddColumn<string>(
            name: "correlation_id",
            schema: "identity_access",
            table: "outbox_messages",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "accounts",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                email = table.Column<string>(type: "text", nullable: false),
                normalized_email = table.Column<string>(type: "text", nullable: false),
                is_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                deactivated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_accounts", x => x.id);
                table.UniqueConstraint("ak_accounts_tenant_id_id", x => new { x.tenant_id, x.id });
            });

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                status_code = table.Column<int>(type: "integer", nullable: false),
                code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                title = table.Column<string>(type: "text", nullable: true),
                created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_idempotency_records", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "credentials",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_credentials", x => x.id);
                table.ForeignKey(
                    name: "FK_credentials_accounts_tenant_id_account_id",
                    columns: x => new { x.tenant_id, x.account_id },
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "verification_tokens",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                expires_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                consumed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_verification_tokens", x => x.id);
                table.ForeignKey(
                    name: "FK_verification_tokens_accounts_tenant_id_account_id",
                    columns: x => new { x.tenant_id, x.account_id },
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumns: new[] { "tenant_id", "id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_accounts_tenant_id_normalized_email",
            schema: "identity_access",
            table: "accounts",
            columns: new[] { "tenant_id", "normalized_email" },
            unique: true,
            filter: "deactivated_on IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_credentials_tenant_id_account_id",
            schema: "identity_access",
            table: "credentials",
            columns: new[] { "tenant_id", "account_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_expires_on",
            schema: "identity_access",
            table: "idempotency_records",
            column: "expires_on");

        migrationBuilder.CreateIndex(
            name: "ux_idempotency_records_scope_key",
            schema: "identity_access",
            table: "idempotency_records",
            columns: new[] { "tenant_id", "operation_id", "key_hash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_verification_tokens_tenant_id_account_id_purpose",
            schema: "identity_access",
            table: "verification_tokens",
            columns: new[] { "tenant_id", "account_id", "purpose" });

        migrationBuilder.CreateIndex(
            name: "ux_verification_tokens_tenant_id_token_hash",
            schema: "identity_access",
            table: "verification_tokens",
            columns: new[] { "tenant_id", "token_hash" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "credentials",
            schema: "identity_access");

        migrationBuilder.DropTable(
            name: "idempotency_records",
            schema: "identity_access");

        migrationBuilder.DropTable(
            name: "verification_tokens",
            schema: "identity_access");

        migrationBuilder.DropTable(
            name: "accounts",
            schema: "identity_access");

        migrationBuilder.DropColumn(
            name: "correlation_id",
            schema: "identity_access",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "exchange",
            schema: "identity_access",
            table: "outbox_messages");
    }
}
