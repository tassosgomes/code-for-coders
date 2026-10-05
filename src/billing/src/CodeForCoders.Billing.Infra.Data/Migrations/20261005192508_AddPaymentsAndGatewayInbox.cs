using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Billing.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddPaymentsAndGatewayInbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "gateway_inbox",
            schema: "billing_access",
            columns: table => new
            {
                id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                @namespace = table.Column<string>(name: "namespace", type: "character varying(255)", maxLength: 255, nullable: false),
                type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                object_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                session_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                payment_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                order_id = table.Column<Guid>(type: "uuid", nullable: true),
                outcome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                method = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                amount_cents = table.Column<int>(type: "integer", nullable: true),
                currency = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_gateway_inbox", x => new { x.@namespace, x.id });
            });

        migrationBuilder.CreateTable(
            name: "payments",
            schema: "billing_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount_cents = table.Column<int>(type: "integer", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                description = table.Column<string>(type: "character varying(263)", maxLength: 263, nullable: false),
                session_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                gateway_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                @namespace = table.Column<string>(name: "namespace", type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payments", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_gateway_inbox_processed_at",
            schema: "billing_access",
            table: "gateway_inbox",
            column: "processed_at",
            filter: "processed_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_payments_namespace_tenant_id_order_id",
            schema: "billing_access",
            table: "payments",
            columns: new[] { "namespace", "tenant_id", "order_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "gateway_inbox",
            schema: "billing_access");

        migrationBuilder.DropTable(
            name: "payments",
            schema: "billing_access");
    }
}
