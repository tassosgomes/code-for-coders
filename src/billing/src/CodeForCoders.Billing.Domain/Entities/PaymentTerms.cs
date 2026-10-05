namespace CodeForCoders.Billing.Domain.Entities;

public sealed record PaymentTerms(Guid StudentId, int AmountCents, string Currency, string Description);
