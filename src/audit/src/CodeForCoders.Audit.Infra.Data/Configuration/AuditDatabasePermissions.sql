-- Execute as the database owner during deploy. The application credential is provisioned outside
-- the repository and must use the role configured by AuditDatabase:WriterRole.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_audit_writer') THEN
        CREATE ROLE code_for_coders_audit_writer NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA audit_access TO code_for_coders_audit_writer;
REVOKE UPDATE, DELETE ON TABLE audit_access.audit_records FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE audit_access.audit_records TO code_for_coders_audit_writer;
REVOKE UPDATE, DELETE ON TABLE audit_access.audit_records FROM code_for_coders_audit_writer;
