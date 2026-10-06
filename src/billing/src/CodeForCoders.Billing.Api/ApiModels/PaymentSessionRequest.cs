namespace CodeForCoders.Billing.Api.ApiModels;

public sealed record PaymentSessionRequest(Guid StudentId, int AmountCents, string Currency, string Description,
 string SuccessUrl, string CancelUrl);
