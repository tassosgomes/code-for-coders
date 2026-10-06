using CodeForCoders.BffAdmin.Api.Clients;
namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record FinanceOrderDetail(Guid OrderId, string Number, OrderStudent Student, string Status, Guid CourseId, string CourseTitle, Guid OfferId, string OfferName, int PriceCents, string Currency, FinanceOrderPeriod AccessPeriod, string? PaymentMethod, string? PaymentReference, int? PaidAmountCents, Guid? GrantId, DateTimeOffset? AccessGrantedAt, DateTimeOffset CreatedAt, DateTimeOffset? PaymentPageExpiresAt, DateTimeOffset? PaidAt, DateTimeOffset? ExpiredAt, DateTimeOffset? CancelledAt)
{
    public static FinanceOrderDetail FromCommerce(CommerceFinanceOrderDetail row, OrderStudent student) => new(row.OrderId, row.Number, student, row.Status, row.CourseId, row.CourseTitle, row.OfferId, row.OfferName, row.PriceCents, row.Currency, row.AccessPeriod, row.PaymentMethod, row.PaymentReference, row.PaidAmountCents, row.GrantId, row.AccessGrantedAt, row.CreatedAt, row.PaymentPageExpiresAt, row.PaidAt, row.ExpiredAt, row.CancelledAt);
}
