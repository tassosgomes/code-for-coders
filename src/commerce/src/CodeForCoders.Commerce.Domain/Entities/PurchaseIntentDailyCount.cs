namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class PurchaseIntentDailyCount
{
    private PurchaseIntentDailyCount() { }
    public Guid TenantId { get; private set; }
    public Guid OfferId { get; private set; }
    public DateOnly Day { get; private set; }
    public long Count { get; private set; }
}
