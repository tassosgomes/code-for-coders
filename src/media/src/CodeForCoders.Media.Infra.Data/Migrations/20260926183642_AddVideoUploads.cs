using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Media.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddVideoUploads : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "operation_idempotency",
            schema: "media_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                response_status_code = table.Column<int>(type: "integer", nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_operation_idempotency", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "video_uploads",
            schema: "media_access",
            columns: table => new
            {
                upload_id = table.Column<Guid>(type: "uuid", nullable: false),
                video_id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                uploader_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                uploader_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                file_size = table.Column<long>(type: "bigint", nullable: false),
                content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                storage_upload_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                part_count = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_video_uploads", x => x.upload_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_operation_idempotency_expires_at",
            schema: "media_access",
            table: "operation_idempotency",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ux_operation_idempotency_scope_key",
            schema: "media_access",
            table: "operation_idempotency",
            columns: new[] { "tenant_id", "actor_account_id", "operation", "idempotency_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_video_uploads_tenant_actor_created_at",
            schema: "media_access",
            table: "video_uploads",
            columns: new[] { "tenant_id", "uploader_account_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_video_uploads_tenant_actor_fingerprint",
            schema: "media_access",
            table: "video_uploads",
            columns: new[] { "tenant_id", "uploader_account_id", "fingerprint" });

        migrationBuilder.CreateIndex(
            name: "ux_video_uploads_video_id",
            schema: "media_access",
            table: "video_uploads",
            column: "video_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "operation_idempotency",
            schema: "media_access");

        migrationBuilder.DropTable(
            name: "video_uploads",
            schema: "media_access");
    }
}
