using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddPublishedContentFingerprint : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "published_fingerprint",
            schema: "content",
            table: "courses",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "published_fingerprint",
            schema: "content",
            table: "courses");
    }
}
