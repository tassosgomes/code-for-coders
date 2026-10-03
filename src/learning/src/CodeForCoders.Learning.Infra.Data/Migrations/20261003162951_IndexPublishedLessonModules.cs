using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class IndexPublishedLessonModules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_course_versions_modules",
            schema: "content",
            table: "course_versions",
            column: "modules")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_course_versions_modules",
            schema: "content",
            table: "course_versions");
    }
}
