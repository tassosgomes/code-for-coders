using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddVideoTags : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tags",
            schema: "media_access",
            columns: table => new
            {
                tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                normalized_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tags", x => x.tag_id);
            });

        migrationBuilder.CreateTable(
            name: "video_tags",
            schema: "media_access",
            columns: table => new
            {
                video_id = table.Column<Guid>(type: "uuid", nullable: false),
                tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_video_tags", x => new { x.video_id, x.tag_id });
                table.ForeignKey(
                    name: "FK_video_tags_tags_tag_id",
                    column: x => x.tag_id,
                    principalSchema: "media_access",
                    principalTable: "tags",
                    principalColumn: "tag_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_video_tags_videos_video_id",
                    column: x => x.video_id,
                    principalSchema: "media_access",
                    principalTable: "videos",
                    principalColumn: "video_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tags_tenant_id_normalized_name",
            schema: "media_access",
            table: "tags",
            columns: new[] { "tenant_id", "normalized_name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_video_tags_tag_id",
            schema: "media_access",
            table: "video_tags",
            column: "tag_id");

        migrationBuilder.CreateIndex(
            name: "IX_video_tags_tenant_id_tag_id",
            schema: "media_access",
            table: "video_tags",
            columns: new[] { "tenant_id", "tag_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "video_tags",
            schema: "media_access");

        migrationBuilder.DropTable(
            name: "tags",
            schema: "media_access");
    }
}
