using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCourseVersionAudience : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "level",
            schema: "content",
            table: "course_versions",
            type: "character varying(12)",
            maxLength: 12,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "prerequisite",
            schema: "content",
            table: "course_versions",
            type: "jsonb",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "level",
            schema: "content",
            table: "course_versions");

        migrationBuilder.DropColumn(
            name: "prerequisite",
            schema: "content",
            table: "course_versions");
    }
}
