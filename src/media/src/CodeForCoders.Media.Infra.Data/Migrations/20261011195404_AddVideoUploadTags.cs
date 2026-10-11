using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddVideoUploadTags : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string[]>(
            name: "tags",
            schema: "media_access",
            table: "video_uploads",
            type: "text[]",
            nullable: false,
            defaultValueSql: "ARRAY[]::text[]");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "tags",
            schema: "media_access",
            table: "video_uploads");
    }
}
