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
    public DateTimeOffset? PaymentPageExpiresAt { get; private set; }
    public DateTimeOffset? PendingPaymentExpiresAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string? PaymentMethod { get; private set; }
    public int? PaidAmountCents { get; private set; }
    public string? GatewayReference { get; private set; }
    public Guid? GrantId { get; private set; }
    public DateTimeOffset? AccessGrantedAt { get; private set; }
    public void EnsurePayable()
    { if (Status != "awaiting-payment") throw new OrderRuleException("ORDER_NOT_PAYABLE", "Order is not payable."); }
    public void OpenPayment(DateTimeOffset expiresAt)
    {
        EnsurePayable();
        if (PendingPaymentExpiresAt is null) PaymentPageExpiresAt = expiresAt;
    }
    public bool RecordPendingPayment(string method, string gatewayReference, DateTimeOffset expiresAt)
    {
        if (Status != "awaiting-payment") return false;
        PaymentMethod = method;
        GatewayReference = gatewayReference;
        PendingPaymentExpiresAt = expiresAt;
        PaymentPageExpiresAt = null;
        return true;
    }
    public bool ConfirmPayment(OrderPaymentConfirmation confirmation)
    {
        if (Status == "paid") return false;
        if (confirmation.AmountCents < 1 || confirmation.Currency != Currency || confirmation.Method is not ("card" or "pix" or "boleto")
            || string.IsNullOrWhiteSpace(confirmation.GatewayReference))
            throw new OrderRuleException("PAYMENT_INVALID", "Payment confirmation is invalid.");
        Status = "paid"; PaidAt = confirmation.ConfirmedAt; PaymentMethod = confirmation.Method;
        PaidAmountCents = confirmation.AmountCents; GatewayReference = confirmation.GatewayReference; return true;
    }
    public void RecordAccess(Guid grantId, DateTimeOffset grantedAt)
    {
        if (grantId == Guid.Empty || Status != "paid") throw new OrderRuleException("ACCESS_INVALID", "Access confirmation is invalid.");
        if (GrantId is not null) return;
        GrantId = grantId; AccessGrantedAt = grantedAt;
    }
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
