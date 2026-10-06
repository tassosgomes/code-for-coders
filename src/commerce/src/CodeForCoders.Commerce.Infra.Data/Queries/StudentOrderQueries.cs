using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class StudentOrderQueries(CommerceDbContext db) : IStudentOrderQueries
{
    public async Task<StudentOrderRows> ListAsync(StudentOrderQuery input, CancellationToken cancellationToken)
    {
        // Orders already carry the frozen conditions; the context scopes both count and rows to the token's school.
        var query = db.Orders.AsNoTracking().Where(order => order.StudentId == input.StudentId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
            .Skip((input.Page - 1) * input.Size).Take(input.Size)
            .Select(order => new StudentOrderSnapshot(
                order.Id, order.Number, order.Status, order.CourseId, order.CourseTitle, order.OfferId, order.OfferName,
                order.PriceCents, order.Currency, order.PeriodType, order.PeriodMonths, order.PaymentMethod,
                order.PendingPaymentExpiresAt, order.PaymentPageExpiresAt, order.AccessGrantedAt, order.CreatedAt,
                order.PaidAt, order.ExpiredAt, order.CancelledAt))
            .ToListAsync(cancellationToken);
        return new(rows, new(input.Page, input.Size, total, (int)Math.Ceiling((double)total / input.Size)));
    }
}
