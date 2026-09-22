using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Notification.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddProcessingNamespace : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_outbox_messages_tenant_id",
            schema: "notification_access",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "ix_delivery_records_status_accepted_on",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropIndex(
            name: "ux_delivery_records_tenant_id_request_id",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropPrimaryKey(
            name: "pk_delivery_outcome_counters",
            schema: "notification_access",
            table: "delivery_outcome_counters");

        migrationBuilder.AddColumn<string>(
            name: "namespace",
            schema: "notification_access",
            table: "outbox_messages",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "default");

        migrationBuilder.AddColumn<string>(
            name: "namespace",
            schema: "notification_access",
            table: "delivery_records",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "default");

        migrationBuilder.AddColumn<string>(
            name: "namespace",
            schema: "notification_access",
            table: "delivery_outcome_counters",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "default");

        migrationBuilder.AddPrimaryKey(
            name: "pk_delivery_outcome_counters",
            schema: "notification_access",
            table: "delivery_outcome_counters",
            columns: new[] { "namespace", "tenant_id", "purpose", "status", "outcome_day" });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_namespace_tenant_id",
            schema: "notification_access",
            table: "outbox_messages",
            columns: new[] { "namespace", "tenant_id" });

        migrationBuilder.CreateIndex(
            name: "ix_delivery_records_namespace_status_accepted_on",
            schema: "notification_access",
            table: "delivery_records",
            columns: new[] { "namespace", "status", "accepted_on" });

        migrationBuilder.CreateIndex(
            name: "ux_delivery_records_namespace_tenant_id_request_id",
            schema: "notification_access",
            table: "delivery_records",
            columns: new[] { "namespace", "tenant_id", "request_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_outbox_messages_namespace_tenant_id",
            schema: "notification_access",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "ix_delivery_records_namespace_status_accepted_on",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropIndex(
            name: "ux_delivery_records_namespace_tenant_id_request_id",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropPrimaryKey(
            name: "pk_delivery_outcome_counters",
            schema: "notification_access",
            table: "delivery_outcome_counters");

        migrationBuilder.DropColumn(
            name: "namespace",
            schema: "notification_access",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "namespace",
            schema: "notification_access",
            table: "delivery_records");

        migrationBuilder.DropColumn(
            name: "namespace",
            schema: "notification_access",
            table: "delivery_outcome_counters");

        migrationBuilder.AddPrimaryKey(
            name: "pk_delivery_outcome_counters",
            schema: "notification_access",
            table: "delivery_outcome_counters",
            columns: new[] { "tenant_id", "purpose", "status", "outcome_day" });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_tenant_id",
            schema: "notification_access",
            table: "outbox_messages",
            column: "tenant_id");

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
}
