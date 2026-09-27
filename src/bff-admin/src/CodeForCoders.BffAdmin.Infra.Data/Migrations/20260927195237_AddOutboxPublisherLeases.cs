using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.BffAdmin.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddOutboxPublisherLeases : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "lease_expires_on",
            schema: "bff_admin_access",
            table: "outbox_messages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "lease_token",
            schema: "bff_admin_access",
            table: "outbox_messages",
            type: "uuid",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "lease_expires_on",
            schema: "bff_admin_access",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "lease_token",
            schema: "bff_admin_access",
            table: "outbox_messages");
    }
}
