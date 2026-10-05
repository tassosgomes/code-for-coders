namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record BillingPaymentRequest(Guid TenantId, Guid OrderId, Guid StudentId, int AmountCents, string Currency,
 string Description, string SuccessUrl, string CancelUrl);
