using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class ExpirePendingVideoUploads : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_video_uploads_tenant_actor_fingerprint",
            schema: "media_access",
            table: "video_uploads");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "expired_at",
            schema: "media_access",
            table: "video_uploads",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_video_uploads_tenant_actor_fingerprint_pending",
            schema: "media_access",
            table: "video_uploads",
            columns: new[] { "tenant_id", "uploader_account_id", "fingerprint" },
            unique: true,
            filter: "completed_at IS NULL AND expired_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_video_uploads_tenant_actor_fingerprint_pending",
            schema: "media_access",
            table: "video_uploads");

        migrationBuilder.DropColumn(
            name: "expired_at",
            schema: "media_access",
            table: "video_uploads");

        migrationBuilder.CreateIndex(
            name: "ix_video_uploads_tenant_actor_fingerprint",
            schema: "media_access",
            table: "video_uploads",
            columns: new[] { "tenant_id", "uploader_account_id", "fingerprint" });
    }
}
