using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.ListFinanceOrders;

public interface IListFinanceOrders : IUseCase<ListFinanceOrdersInput, FinanceOrderPage>;
