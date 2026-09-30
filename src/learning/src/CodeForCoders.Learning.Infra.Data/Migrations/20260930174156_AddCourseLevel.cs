using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCourseLevel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "current_level",
            schema: "content",
            table: "courses",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "level",
            schema: "content",
            table: "courses",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.Sql("UPDATE content.courses SET published_fingerprint = NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "current_level",
            schema: "content",
            table: "courses");

        migrationBuilder.DropColumn(
            name: "level",
            schema: "content",
            table: "courses");
    }
}
