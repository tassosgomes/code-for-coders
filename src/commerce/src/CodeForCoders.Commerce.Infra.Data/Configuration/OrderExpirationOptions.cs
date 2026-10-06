namespace CodeForCoders.Commerce.Infra.Data.Configuration;

public sealed class OrderExpirationOptions
{
    public const string SectionName = "OrderExpiration";
    public bool Enabled { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 100;
}
