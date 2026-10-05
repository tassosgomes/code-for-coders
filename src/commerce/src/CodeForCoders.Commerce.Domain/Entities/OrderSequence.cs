namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class OrderSequence
{
    private OrderSequence() { }
    public Guid TenantId { get; private set; }
    public long Value { get; private set; }
}
