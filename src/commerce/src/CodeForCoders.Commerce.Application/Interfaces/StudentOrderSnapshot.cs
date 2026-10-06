namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentOrderSnapshot(
    Guid OrderId, string Number, string Status, Guid CourseId, string CourseTitle,
    Guid OfferId, string OfferName, int PriceCents, string Currency, string PeriodType, int? PeriodMonths,
    string? PaymentMethod, DateTimeOffset? PendingPaymentExpiresAt, DateTimeOffset? PaymentPageExpiresAt,
    DateTimeOffset? AccessGrantedAt, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt,
    DateTimeOffset? ExpiredAt, DateTimeOffset? CancelledAt);
