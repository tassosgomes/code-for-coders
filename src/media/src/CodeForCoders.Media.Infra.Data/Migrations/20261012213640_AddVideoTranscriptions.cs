using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddVideoTranscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "video_transcription_events",
            schema: "media_access",
            columns: table => new
            {
                event_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_video_transcription_events", x => x.event_id);
            });

        migrationBuilder.CreateTable(
            name: "video_transcriptions",
            schema: "media_access",
            columns: table => new
            {
                transcription_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                video_id = table.Column<Guid>(type: "uuid", nullable: false),
                origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                requested_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                is_current = table.Column<bool>(type: "boolean", nullable: false),
                failure_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                extraction_attempts = table.Column<int>(type: "integer", nullable: false),
                submissions = table.Column<int>(type: "integer", nullable: false),
                submission_failures = table.Column<int>(type: "integer", nullable: false),
                whisper_job_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                deadline_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                next_action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                signaled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                duration_ms = table.Column<long>(type: "bigint", nullable: true),
                segment_count = table.Column<int>(type: "integer", nullable: true),
                audio_deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_video_transcriptions", x => x.transcription_id);
                table.ForeignKey(
                    name: "FK_video_transcriptions_videos_video_id",
                    column: x => x.video_id,
                    principalSchema: "media_access",
                    principalTable: "videos",
                    principalColumn: "video_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "video_transcription_contents",
            schema: "media_access",
            columns: table => new
            {
                transcription_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                duration_ms = table.Column<long>(type: "bigint", nullable: false),
                segments = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_video_transcription_contents", x => x.transcription_id);
                table.ForeignKey(
                    name: "FK_video_transcription_contents_video_transcriptions_transcrip~",
                    column: x => x.transcription_id,
                    principalSchema: "media_access",
                    principalTable: "video_transcriptions",
                    principalColumn: "transcription_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_video_transcription_events_received_at",
            schema: "media_access",
            table: "video_transcription_events",
            column: "received_at");

        migrationBuilder.CreateIndex(
            name: "ix_video_transcriptions_claim",
            schema: "media_access",
            table: "video_transcriptions",
            columns: new[] { "status", "next_action_at" });

        migrationBuilder.CreateIndex(
            name: "IX_video_transcriptions_video_id",
            schema: "media_access",
            table: "video_transcriptions",
            column: "video_id");

        migrationBuilder.CreateIndex(
            name: "ux_video_transcriptions_current",
            schema: "media_access",
            table: "video_transcriptions",
            columns: new[] { "tenant_id", "video_id" },
            unique: true,
            filter: "is_current");

        migrationBuilder.CreateIndex(
            name: "ux_video_transcriptions_ongoing",
            schema: "media_access",
            table: "video_transcriptions",
            columns: new[] { "tenant_id", "video_id" },
            unique: true,
            filter: "status IN ('queued', 'working', 'waiting')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "video_transcription_contents",
            schema: "media_access");

        migrationBuilder.DropTable(
            name: "video_transcription_events",
            schema: "media_access");

        migrationBuilder.DropTable(
            name: "video_transcriptions",
            schema: "media_access");
    }
}
