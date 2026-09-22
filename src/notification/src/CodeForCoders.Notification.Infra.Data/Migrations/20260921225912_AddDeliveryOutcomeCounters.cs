using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Notification.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddDeliveryOutcomeCounters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "recipient",
            schema: "notification_access",
            table: "delivery_records",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(320)",
            oldMaxLength: 320);

        migrationBuilder.CreateTable(
            name: "delivery_outcome_counters",
            schema: "notification_access",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                outcome_day = table.Column<DateOnly>(type: "date", nullable: false),
                count = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_delivery_outcome_counters", x => new { x.tenant_id, x.purpose, x.status, x.outcome_day });
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "delivery_outcome_counters",
            schema: "notification_access");

        migrationBuilder.AlterColumn<string>(
            name: "recipient",
            schema: "notification_access",
            table: "delivery_records",
            type: "character varying(320)",
            maxLength: 320,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(320)",
            oldMaxLength: 320,
            oldNullable: true);
    }
}
