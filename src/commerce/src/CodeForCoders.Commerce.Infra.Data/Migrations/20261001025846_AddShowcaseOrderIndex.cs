using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddShowcaseOrderIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_course_views_showcase_order",
            schema: "catalog",
            table: "course_views",
            columns: new[] { "tenant_id", "in_showcase_since", "course_id" },
            descending: new[] { false, true, false },
            filter: "in_showcase_since IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_course_views_showcase_order",
            schema: "catalog",
            table: "course_views");
    }
}
