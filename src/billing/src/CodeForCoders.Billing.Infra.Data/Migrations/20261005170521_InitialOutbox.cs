using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Billing.Infra.Data.Migrations;
/// <inheritdoc />
public partial class InitialOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "billing_access");

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "billing_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                @namespace = table.Column<string>(name: "namespace", type: "character varying(100)", maxLength: 100, nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                routing_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false),
                last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                trace_parent = table.Column<string>(type: "character varying(55)", maxLength: 55, nullable: true),
                correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_namespace_tenant_id",
            schema: "billing_access",
            table: "outbox_messages",
            columns: new[] { "namespace", "tenant_id" });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_pending",
            schema: "billing_access",
            table: "outbox_messages",
            column: "id",
            filter: "processed_on IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "billing_access");
    }
}
