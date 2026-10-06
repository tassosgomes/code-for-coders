using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class FinanceOrderQueries(CommerceDbContext db) : IFinanceOrderQueries
{
    public async Task<FinanceOrderPage> ListAsync(FinanceOrderQuery input, CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking();
        if (input.Status is not null) query = query.Where(order => order.Status == input.Status);
        if (input.CourseId is { } course) query = query.Where(order => order.CourseId == course);
        if (input.StudentId is { } student) query = query.Where(order => order.StudentId == student);
        if (input.CreatedFrom is { } from) query = query.Where(order => order.CreatedAt >= from);
        if (input.CreatedBefore is { } before) query = query.Where(order => order.CreatedAt < before);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
            .Skip((input.Page - 1) * input.Size).Take(input.Size)
            .Select(order => new FinanceOrderSummary(order.Id, order.Number, order.StudentId, order.Status,
                order.CourseId, order.CourseTitle, order.OfferName, order.PriceCents, order.Currency,
                order.PaymentMethod, order.CreatedAt, order.PaidAt)).ToListAsync(cancellationToken);
        return new(rows, new(input.Page, input.Size, total, (int)Math.Ceiling((double)total / input.Size)));
    }
    public Task<FinanceOrderDetail?> GetAsync(Guid orderId, CancellationToken cancellationToken)
        => db.Orders.AsNoTracking().Where(order => order.Id == orderId)
            .Select(order => new FinanceOrderDetail(order.Id, order.Number, order.StudentId, order.Status,
                order.CourseId, order.CourseTitle, order.OfferId, order.OfferName, order.PriceCents, order.Currency,
                new AccessPeriod(order.PeriodType, order.PeriodMonths), order.PaymentMethod, order.GatewayReference,
                order.PaidAmountCents, order.GrantId, order.AccessGrantedAt, order.CreatedAt,
                order.PaymentPageExpiresAt, order.PaidAt, order.ExpiredAt, order.CancelledAt)).SingleOrDefaultAsync(cancellationToken);
}
