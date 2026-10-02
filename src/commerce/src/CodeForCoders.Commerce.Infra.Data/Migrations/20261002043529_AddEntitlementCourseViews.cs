using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddEntitlementCourseViews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "entitlement");

        migrationBuilder.CreateTable(
            name: "course_views",
            schema: "entitlement",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                normalized_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                version_number = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_course_views1", x => new { x.tenant_id, x.course_id });
            });

        migrationBuilder.CreateIndex(
            name: "IX_course_views_tenant_id_title_course_id1",
            schema: "entitlement",
            table: "course_views",
            columns: new[] { "tenant_id", "title", "course_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "course_views",
            schema: "entitlement");
    }
}
