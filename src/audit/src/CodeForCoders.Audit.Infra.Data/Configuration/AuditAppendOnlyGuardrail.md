# Audit append-only guardrail

`audit` only records events delivered by RabbitMQ. The runtime login configured through
`ConnectionStrings:DefaultConnection` is a member of `code_for_coders_audit_writer`. That group
has only `USAGE` on `audit_access` and `SELECT` and `INSERT` on `audit_access.audit_records`; it has
no `UPDATE`, `DELETE` or `TRUNCATE` grant. The owner login `code_for_coders_audit` is reserved for
the migration step. `AuditDatabasePermissions.sql` documents the deploy grants and revokes.

The database migration also installs a `BEFORE UPDATE OR DELETE` trigger that raises an error.
That trigger protects the invariant even if a privileged connection or an accidental ORM mapping
tries to mutate an existing row. `AuditDbContext` has the same guard before `SaveChanges`, and the
structural test exercises both `Modified` and `Deleted` states without needing a database.

There is intentionally no outbox, producer endpoint or delete/update repository method in this
service. The only write path is the broker consumer appending the consumed event.
