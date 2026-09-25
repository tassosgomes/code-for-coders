using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Identity.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddStaffInvitations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "staff_invitation_id",
            schema: "identity_access",
            table: "idempotency_records",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "superseded_staff_invitation_id",
            schema: "identity_access",
            table: "idempotency_records",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "staff_invitations",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                offered_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                invited_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                accepted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                superseded_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_staff_invitations", x => x.id);
                table.UniqueConstraint("ak_staff_invitations_tenant_id_id", x => new { x.tenant_id, x.id });
            });

        migrationBuilder.CreateIndex(
            name: "ix_staff_invitations_tenant_invited_on",
            schema: "identity_access",
            table: "staff_invitations",
            columns: new[] { "tenant_id", "invited_on" });

        migrationBuilder.CreateIndex(
            name: "ux_staff_invitations_pending_email",
            schema: "identity_access",
            table: "staff_invitations",
            columns: new[] { "tenant_id", "normalized_email" },
            unique: true,
            filter: "accepted_on IS NULL AND superseded_on IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "staff_invitations",
            schema: "identity_access");

        migrationBuilder.DropColumn(
            name: "staff_invitation_id",
            schema: "identity_access",
            table: "idempotency_records");

        migrationBuilder.DropColumn(
            name: "superseded_staff_invitation_id",
            schema: "identity_access",
            table: "idempotency_records");
    }
}
