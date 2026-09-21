namespace CodeForCoders.Audit.Infra.Data;

public static class AuditAppendOnlyPolicy
{
    public const string WriterRoleConfigurationKey = "AuditDatabase:WriterRole";
    public const string DefaultWriterRole = "code_for_coders_audit_writer";
    public const string MutationRejectedMessage = "Audit records are append-only; UPDATE and DELETE are forbidden.";

    public static bool IsMutation(Microsoft.EntityFrameworkCore.EntityState state)
        => state is Microsoft.EntityFrameworkCore.EntityState.Modified
            or Microsoft.EntityFrameworkCore.EntityState.Deleted;
}
