using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.Exceptions;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.GetFinanceOrder;

public sealed class GetFinanceOrder(IFinanceOrderQueries queries) : IGetFinanceOrder
{
    public async Task<FinanceOrderDetail> ExecuteAsync(GetFinanceOrderInput input, CancellationToken cancellationToken)
        => await queries.GetAsync(input.OrderId, cancellationToken) ?? throw new NotFoundException("ORDER_NOT_FOUND");
}
