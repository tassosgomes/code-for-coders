using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCatalogCourseTaglineAndEditReceipts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "tagline",
            schema: "catalog",
            table: "course_views",
            type: "character varying(160)",
            maxLength: 160,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "edit_receipts",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_edit_receipts", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_edit_receipts_expires_at",
            schema: "catalog",
            table: "edit_receipts",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "IX_edit_receipts_tenant_id_actor_id_key",
            schema: "catalog",
            table: "edit_receipts",
            columns: new[] { "tenant_id", "actor_id", "key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "edit_receipts",
            schema: "catalog");

        migrationBuilder.DropColumn(
            name: "tagline",
            schema: "catalog",
            table: "course_views");
    }
}
