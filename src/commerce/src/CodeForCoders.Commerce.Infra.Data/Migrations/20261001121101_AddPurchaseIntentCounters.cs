using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddPurchaseIntentCounters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "purchase_intent_daily_counts",
            schema: "catalog",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                day = table.Column<DateOnly>(type: "date", nullable: false),
                count = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_intent_daily_counts", x => new { x.tenant_id, x.offer_id, x.day });
            });

        migrationBuilder.CreateTable(
            name: "purchase_intent_receipts",
            schema: "catalog",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_intent_receipts", x => new { x.tenant_id, x.offer_id, x.key_hash });
            });

        migrationBuilder.CreateIndex(
            name: "IX_purchase_intent_receipts_expires_at",
            schema: "catalog",
            table: "purchase_intent_receipts",
            column: "expires_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "purchase_intent_daily_counts",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "purchase_intent_receipts",
            schema: "catalog");
    }
}
