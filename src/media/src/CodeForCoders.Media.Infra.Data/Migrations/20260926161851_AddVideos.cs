using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddVideos : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "videos",
            schema: "media_access",
            columns: table => new
            {
                video_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                normalized_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                uploaded_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                uploaded_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                duration_seconds = table.Column<int>(type: "integer", nullable: true),
                failure_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_videos", x => x.video_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_videos_tenant_uploaded_at",
            schema: "media_access",
            table: "videos",
            columns: new[] { "tenant_id", "uploaded_at", "video_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "videos",
            schema: "media_access");
    }
}
