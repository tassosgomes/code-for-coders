namespace CodeForCoders.Commerce.Domain.Entities;

public sealed record OrderPaymentConfirmation(string Method, int AmountCents, string Currency, string GatewayReference, DateTimeOffset ConfirmedAt);
