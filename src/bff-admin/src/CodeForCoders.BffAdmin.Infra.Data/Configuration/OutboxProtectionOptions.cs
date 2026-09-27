namespace CodeForCoders.BffAdmin.Infra.Data.Configuration;

public sealed class OutboxProtectionOptions
{
    public const string SectionName = "OutboxProtection";

    public string KeyBase64 { get; set; } = string.Empty;

    public string KeyVersion { get; set; } = string.Empty;
}
