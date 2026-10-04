using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class CreatePlaybackSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "playback_sessions",
            schema: "media_access",
            columns: table => new
            {
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                video_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_playback_sessions", x => x.session_id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_course_video_references_tenant_id_lesson_id",
            schema: "media_access",
            table: "course_video_references",
            columns: new[] { "tenant_id", "lesson_id" });

        migrationBuilder.CreateIndex(
            name: "IX_playback_sessions_expires_at",
            schema: "media_access",
            table: "playback_sessions",
            column: "expires_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "playback_sessions",
            schema: "media_access");

        migrationBuilder.DropIndex(
            name: "IX_course_video_references_tenant_id_lesson_id",
            schema: "media_access",
            table: "course_video_references");
    }
}
