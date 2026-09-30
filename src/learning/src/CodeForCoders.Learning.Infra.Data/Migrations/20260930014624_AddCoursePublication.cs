using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCoursePublication : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "course_versions",
            schema: "content",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                version_number = table.Column<int>(type: "integer", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                version_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                published_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                published_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                modules = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_course_versions", x => x.id);
                table.ForeignKey(
                    name: "FK_course_versions_courses_course_id",
                    column: x => x.course_id,
                    principalSchema: "content",
                    principalTable: "courses",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_course_versions_course_id",
            schema: "content",
            table: "course_versions",
            column: "course_id");

        migrationBuilder.CreateIndex(
            name: "IX_course_versions_tenant_id_course_id_version_number",
            schema: "content",
            table: "course_versions",
            columns: new[] { "tenant_id", "course_id", "version_number" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "course_versions",
            schema: "content");
    }
}
