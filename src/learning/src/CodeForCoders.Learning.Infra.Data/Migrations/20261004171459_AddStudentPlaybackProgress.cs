using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddStudentPlaybackProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lesson_progress",
            schema: "progress",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                last_position_seconds = table.Column<int>(type: "integer", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                max_position_seconds = table.Column<int>(type: "integer", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lesson_progress", x => new { x.tenant_id, x.student_id, x.lesson_id });
            });

        migrationBuilder.CreateTable(
            name: "playback_advances",
            schema: "progress",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                sequence = table.Column<int>(type: "integer", nullable: false),
                position_seconds = table.Column<int>(type: "integer", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_playback_advances", x => x.event_id);
            });

        migrationBuilder.CreateTable(
            name: "video_durations",
            schema: "progress",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                video_id = table.Column<Guid>(type: "uuid", nullable: false),
                duration_seconds = table.Column<int>(type: "integer", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_video_durations", x => new { x.tenant_id, x.video_id });
            });

        migrationBuilder.CreateIndex(
            name: "IX_lesson_progress_tenant_id_course_id_student_id",
            schema: "progress",
            table: "lesson_progress",
            columns: new[] { "tenant_id", "course_id", "student_id" });

        migrationBuilder.CreateIndex(
            name: "IX_playback_advances_tenant_id_occurred_at",
            schema: "progress",
            table: "playback_advances",
            columns: new[] { "tenant_id", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "IX_playback_advances_tenant_id_student_id_course_id",
            schema: "progress",
            table: "playback_advances",
            columns: new[] { "tenant_id", "student_id", "course_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "lesson_progress",
            schema: "progress");

        migrationBuilder.DropTable(
            name: "playback_advances",
            schema: "progress");

        migrationBuilder.DropTable(
            name: "video_durations",
            schema: "progress");
    }
}
