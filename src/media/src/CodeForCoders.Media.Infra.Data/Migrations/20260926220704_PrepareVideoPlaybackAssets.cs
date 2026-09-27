using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class PrepareVideoPlaybackAssets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "correlation_id",
            schema: "media_access",
            table: "videos",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<byte[]>(
            name: "encrypted_video_key",
            schema: "media_access",
            table: "videos",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "master_key_id",
            schema: "media_access",
            table: "videos",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "next_preparation_at",
            schema: "media_access",
            table: "videos",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "original_deleted_at",
            schema: "media_access",
            table: "videos",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "original_object_key",
            schema: "media_access",
            table: "videos",
            type: "character varying(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<long>(
            name: "original_size_bytes",
            schema: "media_access",
            table: "videos",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<int>(
            name: "preparation_attempts",
            schema: "media_access",
            table: "videos",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<Guid>(
            name: "preparation_lease_id",
            schema: "media_access",
            table: "videos",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "preparation_lease_until",
            schema: "media_access",
            table: "videos",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "stored_bytes",
            schema: "media_access",
            table: "videos",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateIndex(
            name: "ix_videos_original_cleanup",
            schema: "media_access",
            table: "videos",
            columns: new[] { "status", "original_deleted_at" },
            filter: "status = 'ready' AND original_deleted_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_videos_preparation_lease",
            schema: "media_access",
            table: "videos",
            columns: new[] { "status", "preparation_lease_until" },
            filter: "status = 'preparing'");

        migrationBuilder.CreateIndex(
            name: "ix_videos_preparation_queue",
            schema: "media_access",
            table: "videos",
            columns: new[] { "status", "next_preparation_at", "uploaded_at", "video_id" },
            filter: "status = 'received'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_videos_original_cleanup",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropIndex(
            name: "ix_videos_preparation_lease",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropIndex(
            name: "ix_videos_preparation_queue",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "correlation_id",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "encrypted_video_key",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "master_key_id",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "next_preparation_at",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "original_deleted_at",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "original_object_key",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "original_size_bytes",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "preparation_attempts",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "preparation_lease_id",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "preparation_lease_until",
            schema: "media_access",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "stored_bytes",
            schema: "media_access",
            table: "videos");
    }
}
