using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCoursePrerequisite : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "prerequisite_text",
            schema: "content",
            table: "courses",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "recommended_course_ids",
            schema: "content",
            table: "courses",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        migrationBuilder.AddColumn<string>(
            name: "title_search",
            schema: "content",
            table: "courses",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("UPDATE content.courses SET title_search = translate(lower(title), 'áàâãäéèêëíìîïóòôõöúùûüç', 'aaaaaeeeeiiiiooooouuuuc'), published_fingerprint = NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "prerequisite_text",
            schema: "content",
            table: "courses");

        migrationBuilder.DropColumn(
            name: "recommended_course_ids",
            schema: "content",
            table: "courses");

        migrationBuilder.DropColumn(
            name: "title_search",
            schema: "content",
            table: "courses");
    }
}
