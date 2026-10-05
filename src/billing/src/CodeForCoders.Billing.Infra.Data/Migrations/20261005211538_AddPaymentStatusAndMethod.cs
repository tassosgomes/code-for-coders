using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Billing.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddPaymentStatusAndMethod : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "method",
            schema: "billing_access",
            table: "payments",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "status",
            schema: "billing_access",
            table: "payments",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "method",
            schema: "billing_access",
            table: "payments");

        migrationBuilder.DropColumn(
            name: "status",
            schema: "billing_access",
            table: "payments");
    }
}
