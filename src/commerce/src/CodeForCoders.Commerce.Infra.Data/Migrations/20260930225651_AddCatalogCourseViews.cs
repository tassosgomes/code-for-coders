using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCatalogCourseViews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "catalog");

        migrationBuilder.CreateTable(
            name: "course_views",
            schema: "catalog",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                version_number = table.Column<int>(type: "integer", nullable: false),
                source_format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                prerequisite = table.Column<string>(type: "jsonb", nullable: false),
                structure = table.Column<string>(type: "jsonb", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                in_showcase_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_course_views", x => new { x.tenant_id, x.course_id });
            });

        migrationBuilder.CreateIndex(
            name: "IX_course_views_tenant_id_title_course_id",
            schema: "catalog",
            table: "course_views",
            columns: new[] { "tenant_id", "title", "course_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "course_views",
            schema: "catalog");
    }
}
