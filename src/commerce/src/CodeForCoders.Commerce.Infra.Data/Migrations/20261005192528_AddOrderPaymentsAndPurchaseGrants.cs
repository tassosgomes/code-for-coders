using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Commerce.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddOrderPaymentsAndPurchaseGrants : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "access_granted_at",
            schema: "sales",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "gateway_reference",
            schema: "sales",
            table: "orders",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "grant_id",
            schema: "sales",
            table: "orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "paid_amount_cents",
            schema: "sales",
            table: "orders",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "paid_at",
            schema: "sales",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "payment_method",
            schema: "sales",
            table: "orders",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "payment_page_expires_at",
            schema: "sales",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "reason",
            schema: "entitlement",
            table: "access_grants",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500);

        migrationBuilder.AlterColumn<Guid>(
            name: "granted_by",
            schema: "entitlement",
            table: "access_grants",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.CreateIndex(
            name: "IX_access_grants_tenant_id_origin_origin_ref",
            schema: "entitlement",
            table: "access_grants",
            columns: new[] { "tenant_id", "origin", "origin_ref" },
            unique: true,
            filter: "origin_ref IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_access_grants_tenant_id_origin_origin_ref",
            schema: "entitlement",
            table: "access_grants");

        migrationBuilder.DropColumn(
            name: "access_granted_at",
            schema: "sales",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "gateway_reference",
            schema: "sales",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "grant_id",
            schema: "sales",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "paid_amount_cents",
            schema: "sales",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "paid_at",
            schema: "sales",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "payment_method",
            schema: "sales",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "payment_page_expires_at",
            schema: "sales",
            table: "orders");

        migrationBuilder.AlterColumn<string>(
            name: "reason",
            schema: "entitlement",
            table: "access_grants",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500,
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "granted_by",
            schema: "entitlement",
            table: "access_grants",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
