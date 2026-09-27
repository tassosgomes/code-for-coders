namespace CodeForCoders.Audit.Infra.Data.Configuration;

public sealed class AuditSnapshotOptions
{
    public const string SectionName = "AuditSnapshots";

    public string ConnectionString { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "code4coders:audit:snapshot:v1:";
}
