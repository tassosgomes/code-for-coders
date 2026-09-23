namespace CodeForCoders.Identity.Infra.Data.Configuration;

public sealed class OutboxProtectionOptions
{
    public const string SectionName = "OutboxProtection";

    public string KeyBase64 { get; set; } = string.Empty;
}
