namespace CodeForCoders.Billing.Application.Interfaces;

public sealed record GatewaySession(string Reference, string PaymentUrl, DateTimeOffset ExpiresAt);
