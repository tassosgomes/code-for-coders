using CodeForCoders.Commerce.Domain.ValueObjects;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.Common;

public sealed record StudentOrder(Guid OrderId, string Number, string Status, OrderCourse Course, OrderOffer Offer, int PriceCents, string Currency, AccessPeriod AccessPeriod, string? PaymentMethod, object? PendingPayment, DateTimeOffset? PaymentPageExpiresAt, DateTimeOffset? AccessGrantedAt, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, DateTimeOffset? ExpiredAt, DateTimeOffset? CancelledAt)
{
    public static StudentOrder FromOrder(CodeForCoders.Commerce.Domain.Entities.Order order) => new(order.Id, order.Number, order.Status,
        new(order.CourseId, order.CourseTitle), new(order.OfferId, order.OfferName), order.PriceCents, order.Currency,
        new(order.PeriodType, order.PeriodMonths), order.PaymentMethod, null, order.PaymentPageExpiresAt, order.AccessGrantedAt, order.CreatedAt, order.PaidAt, null, null);
}
