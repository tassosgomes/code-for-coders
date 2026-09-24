-- Execute during deploy. Provision the runtime login separately and make it a member of
-- code_for_coders_audit_writer.
REVOKE CONNECT ON DATABASE code_for_coders_audit FROM PUBLIC;
GRANT CONNECT ON DATABASE code_for_coders_audit TO code_for_coders_audit_runtime;
REVOKE ALL PRIVILEGES ON SCHEMA audit_access FROM PUBLIC;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_audit_writer') THEN
        CREATE ROLE code_for_coders_audit_writer NOLOGIN;
    END IF;
END
$$;

ALTER ROLE code_for_coders_audit_writer NOLOGIN;
GRANT USAGE ON SCHEMA audit_access TO code_for_coders_audit_writer;
REVOKE ALL PRIVILEGES ON TABLE audit_access.audit_records FROM PUBLIC;
REVOKE ALL PRIVILEGES ON TABLE audit_access.audit_records FROM code_for_coders_audit_writer;
GRANT SELECT, INSERT ON TABLE audit_access.audit_records TO code_for_coders_audit_writer;
