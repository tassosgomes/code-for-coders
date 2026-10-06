using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.ListFinanceOrders;

public sealed class ListFinanceOrders(IFinanceOrderQueries queries, SchoolTimeZone zone,
    IValidator<ListFinanceOrdersInput> validator) : IListFinanceOrders
{
    public async Task<FinanceOrderPage> ExecuteAsync(ListFinanceOrdersInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        return await queries.ListAsync(new(input.Status, input.CourseId, input.StudentId,
            input.CreatedFrom is { } from ? MidnightUtc(from) : null,
            input.CreatedTo is { } to ? MidnightUtc(to.AddDays(1)) : null, input.Page, input.Size), cancellationToken);
    }
    private DateTimeOffset MidnightUtc(DateOnly day)
        => new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone.Zone));
}
