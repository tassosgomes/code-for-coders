using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.GetFinanceOrder;

public interface IGetFinanceOrder : IUseCase<GetFinanceOrderInput, FinanceOrderDetail>;
