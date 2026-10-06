namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IFinanceOrderQueries
{
    Task<FinanceOrderPage> ListAsync(FinanceOrderQuery input, CancellationToken cancellationToken);
    Task<FinanceOrderDetail?> GetAsync(Guid orderId, CancellationToken cancellationToken);
}
