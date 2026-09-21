using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Audit.Infra.Data.Configuration;

public sealed class AuditDatabaseOptions
{
    public const string SectionName = "AuditDatabase";

    [Required]
    public string WriterRole { get; set; } = AuditAppendOnlyPolicy.DefaultWriterRole;
}
