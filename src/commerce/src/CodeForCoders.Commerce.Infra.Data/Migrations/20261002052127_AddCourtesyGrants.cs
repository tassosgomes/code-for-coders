using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddCourtesyGrants : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "enrollments",
            schema: "entitlement",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                first_granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_enrollments", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "grant_receipts",
            schema: "entitlement",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_grant_receipts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "entitlement",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                routing_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false),
                last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                trace_parent = table.Column<string>(type: "character varying(55)", maxLength: 55, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_entitlement_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "access_grants",
            schema: "entitlement",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                student_id = table.Column<Guid>(type: "uuid", nullable: false),
                course_id = table.Column<Guid>(type: "uuid", nullable: false),
                origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                origin_ref = table.Column<Guid>(type: "uuid", nullable: true),
                period_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                period_months = table.Column<int>(type: "integer", nullable: true),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                expiry_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                expiry_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_access_grants", x => x.id);
                table.ForeignKey(
                    name: "FK_access_grants_enrollments_enrollment_id",
                    column: x => x.enrollment_id,
                    principalSchema: "entitlement",
                    principalTable: "enrollments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_access_grants_enrollment_id",
            schema: "entitlement",
            table: "access_grants",
            column: "enrollment_id");

        migrationBuilder.CreateIndex(
            name: "IX_access_grants_expires_at",
            schema: "entitlement",
            table: "access_grants",
            column: "expires_at",
            filter: "expiry_published_at IS NULL AND expires_at IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_access_grants_tenant_id_student_id_course_id",
            schema: "entitlement",
            table: "access_grants",
            columns: new[] { "tenant_id", "student_id", "course_id" });

        migrationBuilder.CreateIndex(
            name: "IX_access_grants_tenant_id_student_id_granted_at",
            schema: "entitlement",
            table: "access_grants",
            columns: new[] { "tenant_id", "student_id", "granted_at" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "IX_enrollments_tenant_id_student_id_course_id",
            schema: "entitlement",
            table: "enrollments",
            columns: new[] { "tenant_id", "student_id", "course_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_grant_receipts_expires_at",
            schema: "entitlement",
            table: "grant_receipts",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "IX_grant_receipts_tenant_id_actor_id_key_hash",
            schema: "entitlement",
            table: "grant_receipts",
            columns: new[] { "tenant_id", "actor_id", "key_hash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_entitlement_outbox_messages_pending",
            schema: "entitlement",
            table: "outbox_messages",
            column: "id",
            filter: "processed_on IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_entitlement_outbox_messages_tenant_id",
            schema: "entitlement",
            table: "outbox_messages",
            column: "tenant_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "access_grants",
            schema: "entitlement");

        migrationBuilder.DropTable(
            name: "grant_receipts",
            schema: "entitlement");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "entitlement");

        migrationBuilder.DropTable(
            name: "enrollments",
            schema: "entitlement");
    }
}
