using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Identity.Infra.Data.Migrations;

/// <inheritdoc />
public partial class AddStaffSessionIdempotency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "staff_session_id",
            schema: "identity_access",
            table: "idempotency_records",
            type: "uuid",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "staff_session_id",
            schema: "identity_access",
            table: "idempotency_records");
    }
}
