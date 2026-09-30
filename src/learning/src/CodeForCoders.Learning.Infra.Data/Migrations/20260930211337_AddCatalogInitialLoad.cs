using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCatalogInitialLoad : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "message_id",
            schema: "content",
            table: "outbox_messages",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.Sql("UPDATE content.outbox_messages SET message_id = id; ALTER TABLE content.outbox_messages ALTER COLUMN message_id DROP DEFAULT;");

        migrationBuilder.CreateTable(
            name: "catalog_initial_load_executions",
            schema: "content",
            columns: table => new
            {
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                course_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalog_initial_load_executions", x => x.name);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "catalog_initial_load_executions",
            schema: "content");

        migrationBuilder.DropColumn(
            name: "message_id",
            schema: "content",
            table: "outbox_messages");
    }
}
