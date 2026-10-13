using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddTranscriptionAudioCleanups : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "transcription_audio_cleanups",
            schema: "media_access",
            columns: table => new
            {
                transcription_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                video_id = table.Column<Guid>(type: "uuid", nullable: false),
                requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transcription_audio_cleanups", x => x.transcription_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_transcription_audio_cleanups_requested_at_transcription_id",
            schema: "media_access",
            table: "transcription_audio_cleanups",
            columns: new[] { "requested_at", "transcription_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "transcription_audio_cleanups",
            schema: "media_access");
    }
}
