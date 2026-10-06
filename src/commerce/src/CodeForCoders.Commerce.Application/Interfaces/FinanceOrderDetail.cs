using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record FinanceOrderDetail(Guid OrderId, string Number, Guid StudentId, string Status,
    Guid CourseId, string CourseTitle, Guid OfferId, string OfferName, int PriceCents, string Currency,
    AccessPeriod AccessPeriod, string? PaymentMethod, string? PaymentReference, int? PaidAmountCents,
    Guid? GrantId, DateTimeOffset? AccessGrantedAt, DateTimeOffset CreatedAt,
    DateTimeOffset? PaymentPageExpiresAt, DateTimeOffset? PaidAt, DateTimeOffset? ExpiredAt, DateTimeOffset? CancelledAt);
