using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Notification.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddAccountPurchaseReceipts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "receipt_data",
            schema: "notification_access",
            table: "delivery_records",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "recipient_account_id",
            schema: "notification_access",
            table: "delivery_records",
            type: "uuid",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "receipt_data",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropColumn(
            name: "recipient_account_id",
            schema: "notification_access",
            table: "delivery_records");
    }
}
