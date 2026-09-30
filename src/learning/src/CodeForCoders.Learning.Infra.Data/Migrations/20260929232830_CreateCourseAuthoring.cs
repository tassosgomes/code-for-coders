using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class CreateCourseAuthoring : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "content");

        migrationBuilder.CreateTable(
            name: "course_creation_receipts",
            schema: "content",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_course_creation_receipts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "courses",
            schema: "content",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                draft_revision = table.Column<int>(type: "integer", nullable: false),
                current_version = table.Column<int>(type: "integer", nullable: true),
                has_unpublished_changes = table.Column<bool>(type: "boolean", nullable: false),
                created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_edited_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                last_edited_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                last_edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_courses", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_course_creation_receipts_tenant_id_actor_id_key",
            schema: "content",
            table: "course_creation_receipts",
            columns: new[] { "tenant_id", "actor_id", "key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_courses_tenant_id_last_edited_at_id",
            schema: "content",
            table: "courses",
            columns: new[] { "tenant_id", "last_edited_at", "id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "course_creation_receipts",
            schema: "content");

        migrationBuilder.DropTable(
            name: "courses",
            schema: "content");
    }
}
