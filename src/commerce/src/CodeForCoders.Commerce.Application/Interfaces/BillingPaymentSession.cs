namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record BillingPaymentSession(Guid OrderId, string Kind, string PaymentUrl, string? Method, DateTimeOffset ExpiresAt);
