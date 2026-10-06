namespace CodeForCoders.Billing.Domain.Entities;

public sealed class Payment
{
    private Payment() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid StudentId { get; private set; }
    public int AmountCents { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public string Description { get; private set; } = "";
    public string SessionReference { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? GatewayReference { get; private set; }
    public string Status { get; private set; } = "open";
    public string? Method { get; private set; }
    public string? Reason { get; private set; }
    public static Payment Create(Guid tenantId, Guid orderId, PaymentTerms terms)
    {
        if (tenantId == Guid.Empty || orderId == Guid.Empty || terms.StudentId == Guid.Empty || terms.AmountCents < 1
         || terms.Currency != "BRL" || string.IsNullOrWhiteSpace(terms.Description) || terms.Description.Length > 263)
            throw new PaymentRuleException("INVALID_REQUEST", "Payment terms are invalid.");
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            OrderId = orderId,
            StudentId = terms.StudentId,
            AmountCents = terms.AmountCents,
            Currency = terms.Currency,
            Description = terms.Description
        };
    }
    public void EnsureTerms(PaymentTerms terms)
    {
        if (AmountCents != terms.AmountCents || Currency != terms.Currency || Description != terms.Description || StudentId != terms.StudentId)
            throw new PaymentRuleException("PAYMENT_TERMS_CONFLICT", "Payment terms differ from the original request.");
    }
    public void Open(string sessionReference, DateTimeOffset expiresAt, DateTimeOffset now)
    { SessionReference = sessionReference; ExpiresAt = expiresAt; CreatedAt = now; Status = "open"; }
    public void EnsurePayable(DateTimeOffset now)
    {
        if (ConfirmedAt is not null || Status == "confirmed") throw new PaymentRuleException("PAYMENT_ALREADY_CONFIRMED", "Payment was already confirmed.");
        if (Status == "not-confirmed")
        {
            if (Reason == "cancelled")
                throw new PaymentRuleException("PAYMENT_CANCELLED", "Payment was cancelled.");
            throw new PaymentRuleException("PAYMENT_EXPIRED", "Payment page has expired.");
        }
        if (ExpiresAt <= now) throw new PaymentRuleException("PAYMENT_EXPIRED", "Payment page has expired.");
    }
    public bool MarkAwaiting(string method, string gatewayReference, DateTimeOffset expiresAt)
    {
        if (ConfirmedAt is not null || Status == "confirmed") return false;
        // not-confirmed is terminal: a delayed pending notice must not reopen it nor lose its reason.
        if (Status is "awaiting" or "not-confirmed") return false;
        Status = "awaiting";
        Method = method;
        GatewayReference = gatewayReference;
        ExpiresAt = expiresAt;
        return true;
    }
    public bool MarkNotConfirmed(string reason, string? gatewayReference = null)
    {
        if (ConfirmedAt is not null || Status == "confirmed") return false;
        // not-confirmed is terminal for any reason: a later notice must not rewrite it or publish a second fact.
        if (Status == "not-confirmed") return false;
        Status = "not-confirmed";
        Reason = reason;
        if (!string.IsNullOrEmpty(gatewayReference))
            GatewayReference = gatewayReference;
        return true;
    }
    public bool Confirm(string gatewayReference, DateTimeOffset confirmedAt) => Confirm(gatewayReference, null, confirmedAt);
    public bool Confirm(string gatewayReference, string? method, DateTimeOffset confirmedAt)
    {
        if (ConfirmedAt is not null || Status == "confirmed") return false;
        Status = "confirmed";
        if (!string.IsNullOrEmpty(method)) Method = method;
        GatewayReference = gatewayReference; ConfirmedAt = confirmedAt; return true;
    }
}
