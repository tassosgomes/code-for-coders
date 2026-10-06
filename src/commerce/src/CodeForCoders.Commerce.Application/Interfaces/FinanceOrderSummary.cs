namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record FinanceOrderSummary(Guid OrderId, string Number, Guid StudentId, string Status,
    Guid CourseId, string CourseTitle, string OfferName, int PriceCents, string Currency,
    string? PaymentMethod, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt);
