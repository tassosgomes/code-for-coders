using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class BackfillCompletedVideoMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE media_access.videos AS video
            SET original_object_key = upload.object_key,
                original_size_bytes = upload.file_size
            FROM media_access.video_uploads AS upload
            WHERE upload.video_id = video.video_id
              AND upload.completed_at IS NOT NULL
              AND upload.object_key IS NOT NULL
              AND (video.original_object_key = '' OR video.original_size_bytes = 0);
            """);

        migrationBuilder.DropIndex(
            name: "ix_videos_original_cleanup",
            schema: "media_access",
            table: "videos");

        migrationBuilder.CreateIndex(
            name: "ix_videos_original_cleanup",
            schema: "media_access",
            table: "videos",
            columns: new[] { "status", "original_deleted_at" },
            filter: "status IN ('ready', 'failed') AND original_deleted_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_videos_original_cleanup",
            schema: "media_access",
            table: "videos");

        migrationBuilder.CreateIndex(
            name: "ix_videos_original_cleanup",
            schema: "media_access",
            table: "videos",
            columns: new[] { "status", "original_deleted_at" },
            filter: "status = 'ready' AND original_deleted_at IS NULL");

        migrationBuilder.Sql("""
            UPDATE media_access.videos AS video
            SET original_object_key = '',
                original_size_bytes = 0
            FROM media_access.video_uploads AS upload
            WHERE upload.video_id = video.video_id
              AND upload.completed_at IS NOT NULL
              AND video.original_object_key = upload.object_key
              AND video.original_size_bytes = upload.file_size;
            """);
    }
}
