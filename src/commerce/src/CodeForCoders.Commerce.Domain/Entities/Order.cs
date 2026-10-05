namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class Order
{
    private Order() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid StudentId { get; private set; }
    public string Number { get; private set; } = "";
    public string Status { get; private set; } = "awaiting-payment";
    public Guid CourseId { get; private set; }
    public string CourseTitle { get; private set; } = "";
    public Guid OfferId { get; private set; }
    public string OfferName { get; private set; } = "";
    public int PriceCents { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public string PeriodType { get; private set; } = "";
    public int? PeriodMonths { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public static Order Create(Guid tenantId, Guid studentId, OrderCreation creation) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenantId,
        StudentId = studentId,
        Number = creation.Number.ToString("D6", System.Globalization.CultureInfo.InvariantCulture),
        CourseId = creation.Item.CourseId,
        CourseTitle = creation.Item.CourseTitle,
        OfferId = creation.Item.OfferId,
        OfferName = creation.Item.OfferName,
        PriceCents = creation.Item.PriceCents,
        PeriodType = creation.Item.PeriodType,
        PeriodMonths = creation.Item.PeriodMonths,
        CreatedAt = creation.Now
    };
}
