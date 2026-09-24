using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeForCoders.Audit.Infra.Data.Migrations;

/// <inheritdoc />
public partial class SecureAuditRuntimePermissions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
        REVOKE CONNECT ON DATABASE code_for_coders_audit FROM PUBLIC;
        GRANT CONNECT ON DATABASE code_for_coders_audit TO code_for_coders_audit_runtime;

        REVOKE ALL PRIVILEGES ON SCHEMA audit_access FROM PUBLIC;
        GRANT USAGE ON SCHEMA audit_access TO code_for_coders_audit_writer;

        REVOKE ALL PRIVILEGES ON TABLE audit_access.audit_records FROM PUBLIC;
        REVOKE ALL PRIVILEGES ON TABLE audit_access.audit_records FROM code_for_coders_audit_writer;
        GRANT SELECT, INSERT ON TABLE audit_access.audit_records TO code_for_coders_audit_writer;
        """);

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
        REVOKE SELECT, INSERT ON TABLE audit_access.audit_records FROM code_for_coders_audit_writer;
        REVOKE USAGE ON SCHEMA audit_access FROM code_for_coders_audit_writer;
        REVOKE CONNECT ON DATABASE code_for_coders_audit FROM code_for_coders_audit_runtime;
        """);

    }
}
