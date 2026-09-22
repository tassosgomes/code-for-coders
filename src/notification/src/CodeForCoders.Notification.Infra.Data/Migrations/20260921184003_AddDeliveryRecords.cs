using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Notification.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddDeliveryRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "correlation_id",
            schema: "notification_access",
            table: "outbox_messages",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "delivery_records",
            schema: "notification_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                recipient_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                link = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                requested_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                accepted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                delivered_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_delivery_records", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_delivery_records_status_accepted_on",
            schema: "notification_access",
            table: "delivery_records",
            columns: new[] { "status", "accepted_on" });

        migrationBuilder.CreateIndex(
            name: "ux_delivery_records_tenant_id_request_id",
            schema: "notification_access",
            table: "delivery_records",
            columns: new[] { "tenant_id", "request_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "delivery_records",
            schema: "notification_access");

        migrationBuilder.DropColumn(
            name: "correlation_id",
            schema: "notification_access",
            table: "outbox_messages");
    }
}
