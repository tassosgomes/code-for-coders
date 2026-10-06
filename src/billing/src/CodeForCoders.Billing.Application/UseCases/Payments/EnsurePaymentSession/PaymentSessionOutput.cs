namespace CodeForCoders.Billing.Application.UseCases.Payments.EnsurePaymentSession;

public sealed record PaymentSessionOutput(Guid OrderId, string Kind, string PaymentUrl, string? Method, DateTimeOffset ExpiresAt);
