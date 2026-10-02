namespace CodeForCoders.Commerce.Infra.Data.Configuration;

public sealed class AccessExpirationOptions
{
    public const string SectionName = "AccessExpiration";
    public bool Enabled { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 120;
    public int BatchSize { get; set; } = 100;
}
