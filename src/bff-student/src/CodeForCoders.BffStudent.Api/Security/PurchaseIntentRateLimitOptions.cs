namespace CodeForCoders.BffStudent.Api.Security;

public sealed class PurchaseIntentRateLimitOptions
{
    public const string SectionName = "PurchaseIntentRateLimit";
    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}
