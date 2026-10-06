using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.Sales.Common;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.Sales.ListStudentOrders;

public sealed class ListStudentOrders(IStudentOrderQueries queries, IValidator<ListStudentOrdersInput> validator)
    : IListStudentOrders
{
    public async Task<StudentOrderPage> ExecuteAsync(ListStudentOrdersInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var page = await queries.ListAsync(new(input.StudentId, input.Page, input.Size), cancellationToken);
        return new(page.Data.Select(FromSnapshot).ToArray(), page.Pagination);
    }

    private static StudentOrder FromSnapshot(StudentOrderSnapshot row) => new(
        row.OrderId, row.Number, row.Status, new(row.CourseId, row.CourseTitle), new(row.OfferId, row.OfferName),
        row.PriceCents, row.Currency, new(row.PeriodType, row.PeriodMonths), row.PaymentMethod,
        row.Status == "awaiting-payment" && row.PendingPaymentExpiresAt.HasValue && !string.IsNullOrEmpty(row.PaymentMethod)
            ? new PendingPayment(row.PaymentMethod, row.PendingPaymentExpiresAt.Value) : null,
        row.PaymentPageExpiresAt, row.AccessGrantedAt, row.CreatedAt, row.PaidAt, row.ExpiredAt, row.CancelledAt);
}
