using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Notification.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddTransactionalEmailRetryState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "exhausted_attempts",
            schema: "notification_access",
            table: "delivery_records",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "failed_on",
            schema: "notification_access",
            table: "delivery_records",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "last_provider_attempt_on",
            schema: "notification_access",
            table: "delivery_records",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "next_attempt_on",
            schema: "notification_access",
            table: "delivery_records",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "provider_attempt_count",
            schema: "notification_access",
            table: "delivery_records",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "exhausted_attempts",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropColumn(
            name: "failed_on",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropColumn(
            name: "last_provider_attempt_on",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropColumn(
            name: "next_attempt_on",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropColumn(
            name: "provider_attempt_count",
            schema: "notification_access",
            table: "delivery_records");
    }
}
