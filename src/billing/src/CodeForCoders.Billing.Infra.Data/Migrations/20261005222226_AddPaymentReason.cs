using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Billing.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddPaymentReason : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "reason",
            schema: "billing_access",
            table: "payments",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "reason",
            schema: "billing_access",
            table: "gateway_inbox",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "reason",
            schema: "billing_access",
            table: "payments");

        migrationBuilder.DropColumn(
            name: "reason",
            schema: "billing_access",
            table: "gateway_inbox");
    }
}
