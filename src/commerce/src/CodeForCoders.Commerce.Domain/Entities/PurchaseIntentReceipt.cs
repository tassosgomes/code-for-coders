namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class PurchaseIntentReceipt
{
    private PurchaseIntentReceipt() { }
    public Guid TenantId { get; private set; }
    public Guid OfferId { get; private set; }
    public string KeyHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
}
