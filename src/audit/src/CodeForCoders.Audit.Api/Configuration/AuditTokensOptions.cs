namespace CodeForCoders.Audit.Api.Configuration;

public sealed class AuditTokensOptions
{
    public const string SectionName = "AuditTokens";

    public string MetadataAddress { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string Scope { get; set; } = "audit-records:read";
}
