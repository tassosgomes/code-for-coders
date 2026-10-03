using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddPlaybackProgressFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "last_position_seconds",
            schema: "media_access",
            table: "playback_sessions",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "last_progress_at",
            schema: "media_access",
            table: "playback_sessions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "last_sequence",
            schema: "media_access",
            table: "playback_sessions",
            type: "integer",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "last_position_seconds",
            schema: "media_access",
            table: "playback_sessions");

        migrationBuilder.DropColumn(
            name: "last_progress_at",
            schema: "media_access",
            table: "playback_sessions");

        migrationBuilder.DropColumn(
            name: "last_sequence",
            schema: "media_access",
            table: "playback_sessions");
    }
}
