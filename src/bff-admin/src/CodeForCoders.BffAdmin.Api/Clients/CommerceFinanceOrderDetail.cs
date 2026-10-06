using CodeForCoders.BffAdmin.Api.ApiModels;
namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record CommerceFinanceOrderDetail(Guid OrderId, string Number, Guid StudentId, string Status, Guid CourseId, string CourseTitle, Guid OfferId, string OfferName, int PriceCents, string Currency, FinanceOrderPeriod AccessPeriod, string? PaymentMethod, string? PaymentReference, int? PaidAmountCents, Guid? GrantId, DateTimeOffset? AccessGrantedAt, DateTimeOffset CreatedAt, DateTimeOffset? PaymentPageExpiresAt, DateTimeOffset? PaidAt, DateTimeOffset? ExpiredAt, DateTimeOffset? CancelledAt);
