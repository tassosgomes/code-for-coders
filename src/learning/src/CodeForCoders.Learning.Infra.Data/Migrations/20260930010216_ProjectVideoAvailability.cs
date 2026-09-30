using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class ProjectVideoAvailability : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "projected_videos",
            schema: "content",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                video_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_ready = table.Column<bool>(type: "boolean", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_projected_videos", x => new { x.tenant_id, x.video_id });
            });

        migrationBuilder.CreateTable(
            name: "video_fact_receipts",
            schema: "content",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_video_fact_receipts", x => x.event_id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "projected_videos",
            schema: "content");

        migrationBuilder.DropTable(
            name: "video_fact_receipts",
            schema: "content");
    }
}
