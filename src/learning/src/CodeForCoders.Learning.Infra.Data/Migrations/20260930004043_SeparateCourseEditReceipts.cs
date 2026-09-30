using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Learning.Infra.Data.Migrations;

/// <inheritdoc />
public partial class SeparateCourseEditReceipts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "course_edit_receipts",
            schema: "content",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_course_edit_receipts", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_course_edit_receipts_tenant_id_actor_id_key",
            schema: "content",
            table: "course_edit_receipts",
            columns: new[] { "tenant_id", "actor_id", "key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "course_edit_receipts",
            schema: "content");
    }
}
