using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddSalesOrders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "order_receipts",
            schema: "sales",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                status_code = table.Column<int>(type: "integer", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_receipts", x => new { x.tenant_id, x.student_id, x.key_hash });
            });

        migrationBuilder.CreateTable(
            name: "order_sequences",
            schema: "sales",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                value = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_sequences", x => x.tenant_id);
            });

        migrationBuilder.CreateTable(
            name: "orders",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                offer_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                price_cents = table.Column<int>(type: "integer", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                period_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                period_months = table.Column<int>(type: "integer", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_orders", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_order_receipts_expires_at",
            schema: "sales",
            table: "order_receipts",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "IX_orders_tenant_id_number",
            schema: "sales",
            table: "orders",
            columns: new[] { "tenant_id", "number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_orders_pending",
            schema: "sales",
            table: "orders",
            columns: new[] { "tenant_id", "student_id", "offer_id" },
            unique: true,
            filter: "status = 'awaiting-payment'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_receipts",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "order_sequences",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "orders",
            schema: "sales");
    }
}
