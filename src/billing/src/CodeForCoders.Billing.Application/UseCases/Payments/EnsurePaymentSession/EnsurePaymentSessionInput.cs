namespace CodeForCoders.Billing.Application.UseCases.Payments.EnsurePaymentSession;

public sealed record EnsurePaymentSessionInput(Guid OrderId, Guid StudentId, int AmountCents, string Currency,
 string Description, string SuccessUrl, string CancelUrl);
