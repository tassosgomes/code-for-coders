using CodeForCoders.BffAdmin.Api.Clients;
namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record FinanceOrderSummary(Guid OrderId, string Number, OrderStudent Student, string Status, Guid CourseId, string CourseTitle, string OfferName, int PriceCents, string Currency, string? PaymentMethod, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt)
{
    public static FinanceOrderSummary FromCommerce(CommerceFinanceOrderSummary row, OrderStudent student) => new(row.OrderId, row.Number, student, row.Status, row.CourseId, row.CourseTitle, row.OfferName, row.PriceCents, row.Currency, row.PaymentMethod, row.CreatedAt, row.PaidAt);
}
