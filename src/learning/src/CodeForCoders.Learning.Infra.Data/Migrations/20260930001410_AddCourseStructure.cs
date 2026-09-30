using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCourseStructure : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "course_modules",
            schema: "content",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                position = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_course_modules", x => x.id);
                table.ForeignKey(
                    name: "FK_course_modules_courses_course_id",
                    column: x => x.course_id,
                    principalSchema: "content",
                    principalTable: "courses",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "course_lessons",
            schema: "content",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                module_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                position = table.Column<int>(type: "integer", nullable: false),
                video_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_course_lessons", x => x.id);
                table.ForeignKey(
                    name: "FK_course_lessons_course_modules_module_id",
                    column: x => x.module_id,
                    principalSchema: "content",
                    principalTable: "course_modules",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_course_lessons_module_id",
            schema: "content",
            table: "course_lessons",
            column: "module_id");

        migrationBuilder.CreateIndex(
            name: "IX_course_modules_course_id",
            schema: "content",
            table: "course_modules",
            column: "course_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "course_lessons",
            schema: "content");

        migrationBuilder.DropTable(
            name: "course_modules",
            schema: "content");
    }
}
