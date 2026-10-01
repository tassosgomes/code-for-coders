using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCatalogOffers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "offers",
            schema: "catalog",
            columns: table => new
            {
                offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                price_cents = table.Column<int>(type: "integer", nullable: false),
                access_period = table.Column<string>(type: "jsonb", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                offer_revision = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_offers", x => x.offer_id);
                table.ForeignKey(
                    name: "FK_offers_course_views_tenant_id_course_id",
                    columns: x => new { x.tenant_id, x.course_id },
                    principalSchema: "catalog",
                    principalTable: "course_views",
                    principalColumns: new[] { "tenant_id", "course_id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_offers_tenant_id_course_id",
            schema: "catalog",
            table: "offers",
            columns: new[] { "tenant_id", "course_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "offers",
            schema: "catalog");
    }
}
